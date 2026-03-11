using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.DeleteGuild;

public sealed class DeleteGuildHandler
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
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteGuild started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "DeleteGuild failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.Guild.OwnerUserId != callerId)
        {
            _logger.LogWarning(
                "DeleteGuild failed because caller is not the owner. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can delete this guild");
        }

        var guildIconFileId = ctx.Guild.IconFileId;

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _guildRepository.DeleteAsync(guildId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await NotifyGuildDeletedSafelyAsync(new GuildDeletedNotification(guildId));
        await _uploadedFileCleanupService.DeleteIfExistsAsync(guildIconFileId, cancellationToken);

        _logger.LogInformation(
            "DeleteGuild succeeded. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

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
