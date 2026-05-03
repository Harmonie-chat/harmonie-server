using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.DeleteChannel;

public sealed class DeleteChannelHandler : IAuthenticatedHandler<GuildChannelId, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildNotifier _guildNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteChannelHandler> _logger;

    public DeleteChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildNotifier guildNotifier,
        IUnitOfWork unitOfWork,
        ILogger<DeleteChannelHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildNotifier = guildNotifier;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildChannelId request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        if (ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can delete channels");
        }

        if (ctx.Channel.IsDefault)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.CannotDeleteDefault,
                "The default channel cannot be deleted");
        }

        var guildId = ctx.Channel.GuildId;

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildChannelRepository.DeleteAsync(request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _guildNotifier.NotifyChannelDeletedAsync(
                new ChannelDeletedNotification(
                    GuildId: guildId,
                    GuildName: ctx.GuildName ?? string.Empty,
                    ChannelId: request,
                    ChannelName: ctx.Channel.Name),
                ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to send ChannelDeleted notification for channel {ChannelId} in guild {GuildId}",
            request,
            guildId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
