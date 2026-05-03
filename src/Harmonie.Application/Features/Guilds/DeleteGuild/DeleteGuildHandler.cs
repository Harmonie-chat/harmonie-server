using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.DeleteGuild;

public sealed record DeleteGuildInput(GuildId GuildId);

public sealed class DeleteGuildHandler : IAuthenticatedHandler<DeleteGuildInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildRepository _guildRepository;
    private readonly IGuildNotifier _guildNotifier;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteGuildHandler> _logger;

    public DeleteGuildHandler(
        IGuildRepository guildRepository,
        IGuildNotifier guildNotifier,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildNotifier = guildNotifier;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteGuildInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.Guild.OwnerUserId != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can delete this guild");
        }

        var guildIconFileId = ctx.Guild.IconFileId;

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _guildRepository.DeleteAsync(request.GuildId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await NotifyGuildDeletedSafelyAsync(new GuildDeletedNotification(request.GuildId, ctx.Guild.Name.Value));
        await _uploadedFileCleanupService.DeleteIfExistsAsync(guildIconFileId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyGuildDeletedSafelyAsync(
        GuildDeletedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _guildNotifier.NotifyGuildDeletedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "DeleteGuild notification failed (best-effort). GuildId={GuildId}",
            notification.GuildId);
    }
}
