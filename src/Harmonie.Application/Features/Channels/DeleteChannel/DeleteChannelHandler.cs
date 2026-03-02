using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.DeleteChannel;

public sealed class DeleteChannelHandler
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteChannelHandler> _logger;

    public DeleteChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteChannelHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildChannelId channelId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteChannel started. ChannelId={ChannelId}, CallerId={CallerId}",
            channelId,
            callerId);

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "DeleteChannel failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "DeleteChannel failed because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                ctx.Channel.GuildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        if (ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "DeleteChannel failed because caller is not an admin. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}, Role={Role}",
                channelId,
                ctx.Channel.GuildId,
                callerId,
                ctx.CallerRole);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can delete channels");
        }

        if (ctx.Channel.IsDefault)
        {
            _logger.LogWarning(
                "DeleteChannel failed because channel is the default channel. ChannelId={ChannelId}, GuildId={GuildId}",
                channelId,
                ctx.Channel.GuildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.CannotDeleteDefault,
                "The default channel cannot be deleted");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildChannelRepository.DeleteAsync(channelId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "DeleteChannel succeeded. ChannelId={ChannelId}, CallerId={CallerId}",
            channelId,
            callerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
