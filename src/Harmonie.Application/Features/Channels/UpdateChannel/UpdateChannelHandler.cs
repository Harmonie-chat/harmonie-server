using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed class UpdateChannelHandler
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateChannelHandler> _logger;

    public UpdateChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateChannelHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateChannelResponse>> HandleAsync(
        GuildChannelId channelId,
        UserId callerId,
        UpdateChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateChannel started. ChannelId={ChannelId}, CallerId={CallerId}",
            channelId,
            callerId);

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "UpdateChannel failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "UpdateChannel failed because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                ctx.Channel.GuildId,
                callerId);

            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        if (ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "UpdateChannel failed because caller is not an admin. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}, Role={Role}",
                channelId,
                ctx.Channel.GuildId,
                callerId,
                ctx.CallerRole);

            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can update channels");
        }

        var channel = ctx.Channel;

        if (request.Name is not null)
        {
            var nameConflict = await _guildChannelRepository.ExistsByNameInGuildAsync(
                channel.GuildId,
                request.Name.Trim(),
                channelId,
                cancellationToken);

            if (nameConflict)
            {
                _logger.LogWarning(
                    "UpdateChannel failed because a channel with the same name already exists. ChannelId={ChannelId}, GuildId={GuildId}, Name={Name}",
                    channelId,
                    channel.GuildId,
                    request.Name);

                return ApplicationResponse<UpdateChannelResponse>.Fail(
                    ApplicationErrorCodes.Channel.NameConflict,
                    "A channel with this name already exists in this guild");
            }

            var nameResult = channel.UpdateName(request.Name);
            if (nameResult.IsFailure)
            {
                return ApplicationResponse<UpdateChannelResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    nameResult.Error ?? "Channel name update failed");
            }
        }

        if (request.Position is not null)
        {
            var positionResult = channel.UpdatePosition(request.Position.Value);
            if (positionResult.IsFailure)
            {
                return ApplicationResponse<UpdateChannelResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    positionResult.Error ?? "Channel position update failed");
            }
        }

        if (request.Name is not null || request.Position is not null)
        {
            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _guildChannelRepository.UpdateAsync(channel, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        _logger.LogInformation(
            "UpdateChannel succeeded. ChannelId={ChannelId}, CallerId={CallerId}",
            channelId,
            callerId);

        return ApplicationResponse<UpdateChannelResponse>.Ok(new UpdateChannelResponse(
            ChannelId: channel.Id.ToString(),
            GuildId: channel.GuildId.ToString(),
            Name: channel.Name,
            Type: channel.Type.ToString(),
            IsDefault: channel.IsDefault,
            Position: channel.Position));
    }
}
