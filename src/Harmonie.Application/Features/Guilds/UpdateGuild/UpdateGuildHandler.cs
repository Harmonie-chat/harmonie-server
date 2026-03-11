using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed class UpdateGuildHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateGuildHandler> _logger;

    public UpdateGuildHandler(
        IGuildRepository guildRepository,
        IUserRepository userRepository,
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        IUnitOfWork unitOfWork,
        ILogger<UpdateGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _userRepository = userRepository;
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateGuildResponse>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        UpdateGuildRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateGuild started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "UpdateGuild failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<UpdateGuildResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var isCallerOwner = ctx.Guild.OwnerUserId == callerId;
        var isCallerAdmin = ctx.CallerRole == GuildRole.Admin;

        if (!isCallerOwner && !isCallerAdmin)
        {
            _logger.LogWarning(
                "UpdateGuild failed because caller lacks permissions. GuildId={GuildId}, CallerId={CallerId}, Role={Role}",
                guildId,
                callerId,
                ctx.CallerRole);

            return ApplicationResponse<UpdateGuildResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner or an admin can update guild settings");
        }

        var guild = ctx.Guild;
        var previousIconUrl = guild.IconUrl;

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

        if (request.IconUrlIsSet)
        {
            var iconUrlResult = guild.UpdateIconUrl(request.IconUrl);
            if (iconUrlResult.IsFailure)
                return BuildValidationFailure(nameof(request.IconUrl), iconUrlResult);
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
            || request.IconUrlIsSet
            || request.IconColorIsSet
            || request.IconNameIsSet
            || request.IconBgIsSet;
        var shouldDeletePreviousIconFile = request.IconUrlIsSet
            && !string.IsNullOrWhiteSpace(previousIconUrl)
            && !string.Equals(previousIconUrl, guild.IconUrl, StringComparison.Ordinal);

        if (anyFieldSet)
        {
            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _guildRepository.UpdateAsync(guild, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (shouldDeletePreviousIconFile && previousIconUrl is not null)
            await DeleteObsoleteIconFileIfUnreferencedAsync(previousIconUrl, guildId, cancellationToken);

        _logger.LogInformation(
            "UpdateGuild succeeded. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        return ApplicationResponse<UpdateGuildResponse>.Ok(
            new UpdateGuildResponse(
                GuildId: guild.Id.ToString(),
                Name: guild.Name.Value,
                OwnerUserId: guild.OwnerUserId.ToString(),
                IconUrl: guild.IconUrl,
                Icon: BuildIcon(guild)));
    }

    private static GuildIconDto? BuildIcon(Guild guild)
    {
        return guild.IconColor is not null || guild.IconName is not null || guild.IconBg is not null
            ? new GuildIconDto(guild.IconColor, guild.IconName, guild.IconBg)
            : null;
    }

    private async Task DeleteObsoleteIconFileIfUnreferencedAsync(
        string previousIconUrl,
        GuildId guildId,
        CancellationToken cancellationToken)
    {
        if (!UploadedFileUrl.TryParseFileId(previousIconUrl, out var uploadedFileId)
            || uploadedFileId is null)
        {
            return;
        }

        var referencedByAnotherGuild = await _guildRepository.IsIconUrlReferencedByAnotherGuildAsync(
            previousIconUrl,
            guildId,
            cancellationToken);
        if (referencedByAnotherGuild)
            return;

        var referencedByUserAvatar = await _userRepository.ExistsByAvatarUrlAsync(previousIconUrl, cancellationToken);
        if (referencedByUserAvatar)
            return;

        var uploadedFile = await _uploadedFileRepository.GetByIdAsync(uploadedFileId, cancellationToken);
        if (uploadedFile is null)
            return;

        try
        {
            await _objectStorageService.DeleteIfExistsAsync(uploadedFile.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "UpdateGuild icon cleanup failed. GuildId={GuildId}, UploadedFileId={UploadedFileId}, StorageKey={StorageKey}",
                guildId,
                uploadedFileId,
                uploadedFile.StorageKey);
        }
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
