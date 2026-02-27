using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed class CreateGuildHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGuildHandler> _logger;

    public CreateGuildHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateGuildResponse>> HandleAsync(
        CreateGuildRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreateGuild started for user {UserId}",
            currentUserId);

        var guildNameResult = GuildName.Create(request.Name);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuild validation failed for user {UserId}: {Error}",
                currentUserId,
                guildNameResult.Error);

            var details = new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = [guildNameResult.Error ?? "Guild name is invalid"]
            };

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details);
        }

        var guildResult = Guild.Create(guildNameResult.Value, currentUserId);
        if (guildResult.IsFailure || guildResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuild domain creation failed for user {UserId}: {Error}",
                currentUserId,
                guildResult.Error);

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                guildResult.Error ?? "Unable to create guild");
        }

        var ownerMembershipResult = GuildMember.Create(
            guildResult.Value.Id,
            currentUserId,
            GuildRole.Admin,
            invitedByUserId: null);
        if (ownerMembershipResult.IsFailure || ownerMembershipResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuild owner membership creation failed for guild {GuildId}: {Error}",
                guildResult.Value.Id,
                ownerMembershipResult.Error);

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ownerMembershipResult.Error ?? "Unable to create owner membership");
        }

        var defaultTextChannelResult = GuildChannel.Create(
            guildResult.Value.Id,
            "general",
            GuildChannelType.Text,
            isDefault: true,
            position: 0);
        if (defaultTextChannelResult.IsFailure || defaultTextChannelResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuild default text channel creation failed for guild {GuildId}: {Error}",
                guildResult.Value.Id,
                defaultTextChannelResult.Error);

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultTextChannelResult.Error ?? "Unable to create default text channel");
        }

        var defaultVoiceChannelResult = GuildChannel.Create(
            guildResult.Value.Id,
            "General Voice",
            GuildChannelType.Voice,
            isDefault: true,
            position: 1);
        if (defaultVoiceChannelResult.IsFailure || defaultVoiceChannelResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuild default voice channel creation failed for guild {GuildId}: {Error}",
                guildResult.Value.Id,
                defaultVoiceChannelResult.Error);

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultVoiceChannelResult.Error ?? "Unable to create default voice channel");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildRepository.AddAsync(guildResult.Value, cancellationToken);
        var ownerMembershipAdded = await _guildMemberRepository.TryAddAsync(
            ownerMembershipResult.Value,
            cancellationToken);
        if (!ownerMembershipAdded)
        {
            _logger.LogWarning(
                "CreateGuild owner membership insert failed for guild {GuildId}",
                guildResult.Value.Id);

            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Owner membership could not be created.");
        }

        await _guildChannelRepository.AddAsync(defaultTextChannelResult.Value, cancellationToken);
        await _guildChannelRepository.AddAsync(defaultVoiceChannelResult.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "CreateGuild succeeded. GuildId={GuildId}, OwnerUserId={OwnerUserId}, DefaultTextChannelId={DefaultTextChannelId}, DefaultVoiceChannelId={DefaultVoiceChannelId}",
            guildResult.Value.Id,
            guildResult.Value.OwnerUserId,
            defaultTextChannelResult.Value.Id,
            defaultVoiceChannelResult.Value.Id);

        var payload = new CreateGuildResponse(
            GuildId: guildResult.Value.Id.ToString(),
            Name: guildResult.Value.Name.Value,
            OwnerUserId: guildResult.Value.OwnerUserId.ToString(),
            DefaultTextChannelId: defaultTextChannelResult.Value.Id.ToString(),
            DefaultVoiceChannelId: defaultVoiceChannelResult.Value.Id.ToString(),
            CreatedAtUtc: guildResult.Value.CreatedAtUtc);

        return ApplicationResponse<CreateGuildResponse>.Ok(payload);
    }
}
