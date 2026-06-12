using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed record CreateGuildInviteInput(GuildId GuildId, int? MaxUses = null, int? ExpiresInHours = null);

public sealed class CreateGuildInviteHandler : IAuthenticatedHandler<CreateGuildInviteInput, CreateGuildInviteResponse>
{
    private const int MaxCodeGenerationAttempts = 3;

    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGuildInviteHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<CreateGuildInviteResponse>> HandleAsync(
        CreateGuildInviteInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(input.GuildId, currentUserId, cancellationToken);
        if (guildAccess is null)
        {
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can create invite links");
        }

        GuildInvite? invite = null;

        // Each attempt generates a fresh random code; a unique-constraint
        // collision on the code is retried with a new transaction scope.
        for (var attempt = 0; attempt < MaxCodeGenerationAttempts && invite is null; attempt++)
        {
            var inviteResult = GuildInvite.Create(input.GuildId, currentUserId, input.MaxUses, input.ExpiresInHours);
            if (inviteResult.IsFailure || inviteResult.Value is null)
            {
                return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    inviteResult.Error ?? "Unable to create guild invite");
            }

            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            var added = await _guildInviteRepository.TryAddAsync(inviteResult.Value, cancellationToken);
            if (!added)
                continue;

            await transaction.CommitAsync(cancellationToken);
            invite = inviteResult.Value;
        }

        if (invite is null)
        {
            throw new InvalidOperationException(
                $"Failed to generate a unique invite code after {MaxCodeGenerationAttempts} attempts.");
        }

        var payload = new CreateGuildInviteResponse(
            InviteId: invite.Id.Value,
            Code: invite.Code,
            GuildId: input.GuildId.Value,
            CreatorId: currentUserId.Value,
            MaxUses: invite.MaxUses,
            UsesCount: invite.UsesCount,
            ExpiresAtUtc: invite.ExpiresAtUtc,
            CreatedAtUtc: invite.CreatedAtUtc);

        return ApplicationResponse<CreateGuildInviteResponse>.Ok(payload);
    }
}
