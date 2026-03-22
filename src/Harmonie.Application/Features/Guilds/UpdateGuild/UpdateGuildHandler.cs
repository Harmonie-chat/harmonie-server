using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Guilds;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed record UpdateGuildInput(GuildId GuildId, UpdateGuildRequest Request);

public sealed class UpdateGuildHandler
    : IAuthenticatedHandler<UpdateGuildInput, UpdateGuildResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGuildHandler(
        IGuildRepository guildRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<UpdateGuildResponse>> HandleAsync(
        UpdateGuildInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var request = input.Request;

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

        if (request.NameIsSet)
        {
            var guildNameResult = GuildName.Create(request.Name);
            if (guildNameResult.IsFailure || guildNameResult.Value is null)
            {
                return BuildValidationFailure(
                    nameof(request.Name),
                    guildNameResult.Error ?? "Guild name is invalid");
            }

            var updateResult = guild.UpdateName(guildNameResult.Value);
            if (updateResult.IsFailure)
                return BuildValidationFailure(nameof(request.Name), updateResult);
        }

        if (request.IconFileIdIsSet)
        {
            if (!TryParseUploadedFileId(request.IconFileId, out var iconFileId))
            {
                return BuildValidationFailure(
                    nameof(request.IconFileId),
                    "Guild icon file ID is invalid");
            }

            var iconFileResult = guild.UpdateIconFile(iconFileId);
            if (iconFileResult.IsFailure)
                return BuildValidationFailure(nameof(request.IconFileId), iconFileResult);
        }

        if (request.IconColorIsSet)
        {
            var iconColorResult = guild.UpdateIconColor(request.IconColor);
            if (iconColorResult.IsFailure)
                return BuildValidationFailure("Icon.Color", iconColorResult);
        }

        if (request.IconNameIsSet)
        {
            var iconNameResult = guild.UpdateIconName(request.IconName);
            if (iconNameResult.IsFailure)
                return BuildValidationFailure("Icon.Name", iconNameResult);
        }

        if (request.IconBgIsSet)
        {
            var iconBgResult = guild.UpdateIconBg(request.IconBg);
            if (iconBgResult.IsFailure)
                return BuildValidationFailure("Icon.Bg", iconBgResult);
        }

        var anyFieldSet = request.NameIsSet
            || request.IconFileIdIsSet
            || request.IconColorIsSet
            || request.IconNameIsSet
            || request.IconBgIsSet;
        var shouldDeletePreviousIconFile = request.IconFileIdIsSet
            && previousIconFileId is not null
            && previousIconFileId != guild.IconFileId;

        if (anyFieldSet)
        {
            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _guildRepository.UpdateAsync(guild, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (shouldDeletePreviousIconFile)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousIconFileId, cancellationToken);

        return ApplicationResponse<UpdateGuildResponse>.Ok(
            new UpdateGuildResponse(
                GuildId: guild.Id.ToString(),
                Name: guild.Name.Value,
                OwnerUserId: guild.OwnerUserId.ToString(),
                IconFileId: guild.IconFileId?.ToString(),
                Icon: BuildIcon(guild)));
    }

    private static GuildIconDto? BuildIcon(Guild guild)
    {
        return guild.IconColor is not null || guild.IconName is not null || guild.IconBg is not null
            ? new GuildIconDto(guild.IconColor, guild.IconName, guild.IconBg)
            : null;
    }

    private static bool TryParseUploadedFileId(string? fileId, out UploadedFileId? uploadedFileId)
    {
        if (fileId is null)
        {
            uploadedFileId = null;
            return true;
        }

        return UploadedFileId.TryParse(fileId, out uploadedFileId);
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
