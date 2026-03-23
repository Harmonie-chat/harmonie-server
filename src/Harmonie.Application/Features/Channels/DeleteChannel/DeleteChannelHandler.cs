using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.DeleteChannel;

public sealed class DeleteChannelHandler : IAuthenticatedHandler<GuildChannelId, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork)
    {
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
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

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildChannelRepository.DeleteAsync(request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
