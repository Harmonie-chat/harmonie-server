using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed record UpdateChannelInput(GuildChannelId ChannelId, string? Name = null, int? Position = null);

public sealed class UpdateChannelHandler : IAuthenticatedHandler<UpdateChannelInput, UpdateChannelResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork)
    {
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<UpdateChannelResponse>> HandleAsync(
        UpdateChannelInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        if (ctx.CallerRole != GuildRole.Admin)
        {
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
                request.ChannelId,
                cancellationToken);

            if (nameConflict)
            {
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

        return ApplicationResponse<UpdateChannelResponse>.Ok(new UpdateChannelResponse(
            ChannelId: channel.Id.Value,
            GuildId: channel.GuildId.Value,
            Name: channel.Name,
            Type: channel.Type.ToString(),
            IsDefault: channel.IsDefault,
            Position: channel.Position));
    }
}
