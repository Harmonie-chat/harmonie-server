using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed class CreateGuildHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGuildHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<CreateGuildResponse>> HandleAsync(
        CreateGuildRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (currentUserId is null)
            throw new ArgumentNullException(nameof(currentUserId));

        var guildNameResult = GuildName.Create(request.Name);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
        {
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
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                guildResult.Error ?? "Unable to create guild");

        var ownerMembershipResult = GuildMember.Create(
            guildResult.Value.Id,
            currentUserId,
            GuildRole.Admin,
            invitedByUserId: null);
        if (ownerMembershipResult.IsFailure || ownerMembershipResult.Value is null)
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ownerMembershipResult.Error ?? "Unable to create owner membership");

        var defaultTextChannelResult = GuildChannel.Create(
            guildResult.Value.Id,
            "general",
            GuildChannelType.Text,
            isDefault: true,
            position: 0);
        if (defaultTextChannelResult.IsFailure || defaultTextChannelResult.Value is null)
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultTextChannelResult.Error ?? "Unable to create default text channel");

        var defaultVoiceChannelResult = GuildChannel.Create(
            guildResult.Value.Id,
            "General Voice",
            GuildChannelType.Voice,
            isDefault: true,
            position: 1);
        if (defaultVoiceChannelResult.IsFailure || defaultVoiceChannelResult.Value is null)
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                defaultVoiceChannelResult.Error ?? "Unable to create default voice channel");

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildRepository.AddAsync(guildResult.Value, cancellationToken);
        var ownerMembershipAdded = await _guildMemberRepository.TryAddAsync(
            ownerMembershipResult.Value,
            cancellationToken);
        if (!ownerMembershipAdded)
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Owner membership could not be created.");

        await _guildChannelRepository.AddAsync(defaultTextChannelResult.Value, cancellationToken);
        await _guildChannelRepository.AddAsync(defaultVoiceChannelResult.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
