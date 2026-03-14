using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.DeleteGuildIcon;

public sealed class DeleteGuildIconHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteGuildIconHandler> _logger;

    public DeleteGuildIconHandler(
        IGuildRepository guildRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteGuildIconHandler> logger)
    {
        _guildRepository = guildRepository;
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
            "DeleteGuildIcon started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "DeleteGuildIcon failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var isCallerOwner = ctx.Guild.OwnerUserId == callerId;
        var isCallerAdmin = ctx.CallerRole == GuildRole.Admin;
        if (!isCallerOwner && !isCallerAdmin)
        {
            _logger.LogWarning(
                "DeleteGuildIcon failed because caller lacks permissions. GuildId={GuildId}, CallerId={CallerId}, Role={Role}",
                guildId,
                callerId,
                ctx.CallerRole);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner or an admin can delete the guild icon");
        }

        var previousIconFileId = ctx.Guild.IconFileId;
        if (previousIconFileId is null)
        {
            _logger.LogWarning(
                "DeleteGuildIcon failed because no icon is set. GuildId={GuildId}",
                guildId);

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

        _logger.LogInformation(
            "DeleteGuildIcon succeeded. GuildId={GuildId}, CallerId={CallerId}, DeletedIconFileId={IconFileId}",
            guildId,
            callerId,
            previousIconFileId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
