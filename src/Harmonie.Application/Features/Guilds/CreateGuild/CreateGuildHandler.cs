using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds;
using Harmonie.Application.Interfaces.Channels;
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

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed class CreateGuildHandler : IAuthenticatedHandler<CreateGuildRequest, CreateGuildResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateGuildHandler> _logger;

    public CreateGuildHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildChannelRepository guildChannelRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CreateGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildChannelRepository = guildChannelRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateGuildResponse>> HandleAsync(
        CreateGuildRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var guildNameResult = GuildName.Create(request.Name);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.Name),
                    ApplicationErrorCodes.Validation.Invalid,
                    guildNameResult.Error ?? "Guild name is invalid"));
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var guildResult = Guild.Create(guildNameResult.Value, currentUserId, nowUtc);
        if (guildResult.IsFailure || guildResult.Value is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                guildResult.Error ?? "Unable to create guild");
        }

        var guild = guildResult.Value;

        if (request.IconFileId.HasValue)
        {
            var iconFileResult = guild.UpdateIconFile(UploadedFileId.From(request.IconFileId.Value), nowUtc);
            if (iconFileResult.IsFailure)
                return BuildIconValidationFailure(nameof(request.IconFileId), iconFileResult);
        }

        if (request.Icon is not null && (request.Icon.Color is not null || request.Icon.Name is not null || request.Icon.Bg is not null))
        {
            var appearanceResult = Appearance.Create(request.Icon.Color, request.Icon.Name, request.Icon.Bg);
            if (appearanceResult.IsFailure || appearanceResult.Value is null)
                return BuildIconValidationFailure("Icon", appearanceResult.Error);

            guild.UpdateIcon(appearanceResult.Value, nowUtc);
        }

        var ownerMembershipResult = GuildMember.Create(
            guild.Id,
            currentUserId,
            GuildRole.Admin,
            invitedByUserId: null,
            joinedAtUtc: nowUtc);
        if (ownerMembershipResult.IsFailure || ownerMembershipResult.Value is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ownerMembershipResult.Error ?? "Unable to create owner membership");
        }

        var defaultTextChannelResult = GuildChannel.Create(
            guild.Id,
            "general",
            GuildChannelType.Text,
            isDefault: true,
            position: 0,
            createdAtUtc: nowUtc);
        if (defaultTextChannelResult.IsFailure || defaultTextChannelResult.Value is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultTextChannelResult.Error ?? "Unable to create default text channel");
        }

        var defaultVoiceChannelResult = GuildChannel.Create(
            guild.Id,
            "General Voice",
            GuildChannelType.Voice,
            isDefault: true,
            position: 1,
            createdAtUtc: nowUtc);
        if (defaultVoiceChannelResult.IsFailure || defaultVoiceChannelResult.Value is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultVoiceChannelResult.Error ?? "Unable to create default voice channel");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildRepository.AddAsync(guild, cancellationToken);
        var ownerMembershipAdded = await _guildMemberRepository.TryAddAsync(
            ownerMembershipResult.Value,
            cancellationToken);
        if (!ownerMembershipAdded)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Owner membership could not be created.");
        }

        await _guildChannelRepository.AddAsync(defaultTextChannelResult.Value, cancellationToken);
        await _guildChannelRepository.AddAsync(defaultVoiceChannelResult.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.AddUserToGuildGroupsAsync(currentUserId, guild.Id, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to subscribe user {UserId} to guild {GuildId} SignalR groups",
            currentUserId,
            guild.Id);

        var payload = new CreateGuildResponse(
            GuildId: guild.Id.Value,
            Name: guild.Name.Value,
            OwnerUserId: guild.OwnerUserId.Value,
            IconFileId: guild.IconFileId?.Value,
            Icon: BuildIcon(guild),
            DefaultTextChannelId: defaultTextChannelResult.Value.Id.Value,
            DefaultVoiceChannelId: defaultVoiceChannelResult.Value.Id.Value,
            CreatedAtUtc: guild.CreatedAtUtc);

        return ApplicationResponse<CreateGuildResponse>.Ok(payload);
    }

    private static GuildIconDto? BuildIcon(Guild guild)
    {
        return guild.Icon.HasValue
            ? new GuildIconDto(guild.Icon.Color, guild.Icon.Glyph, guild.Icon.Bg)
            : null;
    }

    private static ApplicationResponse<CreateGuildResponse> BuildIconValidationFailure(
        string propertyName,
        string? detail)
    {
        return ApplicationResponse<CreateGuildResponse>.Fail(
            ApplicationErrorCodes.Common.ValidationFailed,
            "Request validation failed",
            EndpointExtensions.SingleValidationError(
                propertyName,
                ApplicationErrorCodes.Validation.Invalid,
                detail ?? "Guild field is invalid"));
    }

    private static ApplicationResponse<CreateGuildResponse> BuildIconValidationFailure(
        string propertyName,
        Result result)
        => BuildIconValidationFailure(propertyName, result.Error);
}
