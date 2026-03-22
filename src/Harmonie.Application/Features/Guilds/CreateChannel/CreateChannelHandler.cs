using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed record CreateChannelInput(GuildId GuildId, string Name, GuildChannelType ChannelType, int Position);

public sealed class CreateChannelHandler : IAuthenticatedHandler<CreateChannelInput, CreateChannelResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateChannelHandler> _logger;

    public CreateChannelHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IUnitOfWork unitOfWork,
        ILogger<CreateChannelHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateChannelResponse>> HandleAsync(
        CreateChannelInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can create channels");
        }

        var channelResult = GuildChannel.Create(request.GuildId, request.Name, request.ChannelType, isDefault: false, request.Position);
        if (channelResult.IsFailure || channelResult.Value is null)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                channelResult.Error ?? "Channel creation failed");
        }

        var channel = channelResult.Value;

        var nameConflict = await _guildChannelRepository.ExistsByNameInGuildAsync(
            request.GuildId,
            channel.Name,
            channel.Id,
            cancellationToken);
        if (nameConflict)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NameConflict,
                "A channel with this name already exists in this guild");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildChannelRepository.AddAsync(channel, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (channel.Type == GuildChannelType.Text)
        {
            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _realtimeGroupManager.AddAllGuildMembersToChannelGroupAsync(request.GuildId, channel.Id, ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to subscribe guild {GuildId} members to channel {ChannelId} SignalR group",
                request.GuildId,
                channel.Id);
        }

        return ApplicationResponse<CreateChannelResponse>.Ok(new CreateChannelResponse(
            ChannelId: channel.Id.ToString(),
            GuildId: request.GuildId.ToString(),
            Name: channel.Name,
            Type: channel.Type.ToString(),
            IsDefault: channel.IsDefault,
            Position: channel.Position));
    }
}
