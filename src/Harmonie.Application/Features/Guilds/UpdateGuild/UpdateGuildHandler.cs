using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Guilds;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed record UpdateGuildInput(
    GuildId GuildId,
    string? Name,
    Guid? IconFileId,
    string? IconColor,
    string? IconName,
    string? IconBg,
    bool NameIsSet,
    bool IconFileIdIsSet,
    bool IconColorIsSet,
    bool IconNameIsSet,
    bool IconBgIsSet);

public sealed class UpdateGuildHandler
    : IAuthenticatedHandler<UpdateGuildInput, UpdateGuildResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuildNotifier _guildNotifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateGuildHandler> _logger;

    public UpdateGuildHandler(
        IGuildRepository guildRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        IGuildNotifier guildNotifier,
        TimeProvider timeProvider,
        ILogger<UpdateGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _guildNotifier = guildNotifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateGuildResponse>> HandleAsync(
        UpdateGuildInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(input.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<UpdateGuildResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var isCallerOwner = ctx.Guild.OwnerUserId == currentUserId;
        var isCallerAdmin = ctx.CallerRole == GuildRole.Admin;

        if (!isCallerOwner && !isCallerAdmin)
        {
            return ApplicationResponse<UpdateGuildResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner or an admin can update guild settings");
        }

        var guild = ctx.Guild;
        var previousIconFileId = guild.IconFileId;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (input.NameIsSet)
        {
            var guildNameResult = GuildName.Create(input.Name);
            if (guildNameResult.IsFailure || guildNameResult.Value is null)
            {
                return BuildValidationFailure(
                    nameof(input.Name),
                    guildNameResult.Error ?? "Guild name is invalid");
            }

            var updateResult = guild.UpdateName(guildNameResult.Value, nowUtc);
            if (updateResult.IsFailure)
                return BuildValidationFailure(nameof(input.Name), updateResult);
        }

        if (input.IconFileIdIsSet)
        {
            var iconFileId = input.IconFileId.HasValue ? UploadedFileId.From(input.IconFileId.Value) : null;
            var iconFileResult = guild.UpdateIconFile(iconFileId, nowUtc);
            if (iconFileResult.IsFailure)
                return BuildValidationFailure(nameof(input.IconFileId), iconFileResult);
        }

        if (input.IconColorIsSet || input.IconNameIsSet || input.IconBgIsSet)
        {
            var newAppearanceResult = Appearance.Create(
                input.IconColorIsSet ? input.IconColor : guild.Icon.Color,
                input.IconNameIsSet ? input.IconName : guild.Icon.Glyph,
                input.IconBgIsSet ? input.IconBg : guild.Icon.Bg);

            if (newAppearanceResult.IsFailure || newAppearanceResult.Value is null)
                return BuildValidationFailure("Icon", newAppearanceResult.Error ?? "Icon appearance is invalid");

            guild.UpdateIcon(newAppearanceResult.Value, nowUtc);
        }

        var anyFieldSet = input.NameIsSet
            || input.IconFileIdIsSet
            || input.IconColorIsSet
            || input.IconNameIsSet
            || input.IconBgIsSet;
        var shouldDeletePreviousIconFile = input.IconFileIdIsSet
            && previousIconFileId is not null
            && previousIconFileId != guild.IconFileId;

        if (anyFieldSet)
        {
            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _guildRepository.UpdateAsync(guild, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _guildNotifier.NotifyGuildUpdatedAsync(
                    new GuildUpdatedNotification(guild.Id, guild.Name.Value, guild.IconFileId),
                    ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to notify guild {GuildId} that settings were updated",
                guild.Id);
        }

        if (shouldDeletePreviousIconFile)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousIconFileId, cancellationToken);

        return ApplicationResponse<UpdateGuildResponse>.Ok(
            new UpdateGuildResponse(
                GuildId: guild.Id.Value,
                Name: guild.Name.Value,
                OwnerUserId: guild.OwnerUserId.Value,
                IconFileId: guild.IconFileId?.Value,
                Icon: BuildIcon(guild)));
    }

    private static GuildIconDto? BuildIcon(Guild guild)
    {
        return guild.Icon.HasValue
            ? new GuildIconDto(guild.Icon.Color, guild.Icon.Glyph, guild.Icon.Bg)
            : null;
    }

    private static ApplicationResponse<UpdateGuildResponse> BuildValidationFailure(
        string propertyName,
        string detail)
    {
        return ApplicationResponse<UpdateGuildResponse>.Fail(
            ApplicationErrorCodes.Common.ValidationFailed,
            "Request validation failed",
            EndpointExtensions.SingleValidationError(
                propertyName,
                ApplicationErrorCodes.Validation.Invalid,
                detail));
    }

    private static ApplicationResponse<UpdateGuildResponse> BuildValidationFailure(
        string propertyName,
        Result result)
    {
        return BuildValidationFailure(propertyName, result.Error ?? "Guild field is invalid");
    }
}
