using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.DeleteGuildIcon;

public sealed record DeleteGuildIconInput(GuildId GuildId);

public sealed class DeleteGuildIconHandler : IAuthenticatedHandler<DeleteGuildIconInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuildNotifier _guildNotifier;
    private readonly ILogger<DeleteGuildIconHandler> _logger;

    public DeleteGuildIconHandler(
        IGuildRepository guildRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        IGuildNotifier guildNotifier,
        ILogger<DeleteGuildIconHandler> logger)
    {
        _guildRepository = guildRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _guildNotifier = guildNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteGuildIconInput request,
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

        var isCallerOwner = ctx.Guild.OwnerUserId == currentUserId;
        var isCallerAdmin = ctx.CallerRole == GuildRole.Admin;
        if (!isCallerOwner && !isCallerAdmin)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner or an admin can delete the guild icon");
        }

        var previousIconFileId = ctx.Guild.IconFileId;
        if (previousIconFileId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Upload.NotFound,
                "Guild icon was not found");
        }

        var iconFileResult = ctx.Guild.UpdateIconFile(null);
        if (iconFileResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                iconFileResult.Error ?? "Guild icon file is invalid");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _guildRepository.UpdateAsync(ctx.Guild, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(previousIconFileId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _guildNotifier.NotifyGuildUpdatedAsync(
                new GuildUpdatedNotification(ctx.Guild.Id, ctx.Guild.Name.Value, null),
                ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to notify guild {GuildId} that icon was deleted",
            ctx.Guild.Id);

        return ApplicationResponse<bool>.Ok(true);
    }
}
