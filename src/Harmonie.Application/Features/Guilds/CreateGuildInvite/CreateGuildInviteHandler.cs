using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed record CreateGuildInviteInput(GuildId GuildId, CreateGuildInviteRequest Request);

public sealed class CreateGuildInviteHandler : IAuthenticatedHandler<CreateGuildInviteInput, CreateGuildInviteResponse>
{
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
        var (guildId, request) = input;

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
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

        var inviteResult = GuildInvite.Create(guildId, currentUserId, request.MaxUses, request.ExpiresInHours);
        if (inviteResult.IsFailure || inviteResult.Value is null)
        {
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                inviteResult.Error ?? "Unable to create guild invite");
        }

        var invite = inviteResult.Value;

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildInviteRepository.AddAsync(invite, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var payload = new CreateGuildInviteResponse(
            InviteId: invite.Id.ToString(),
            Code: invite.Code,
            GuildId: guildId.ToString(),
            CreatorId: currentUserId.ToString(),
            MaxUses: invite.MaxUses,
            UsesCount: invite.UsesCount,
            ExpiresAtUtc: invite.ExpiresAtUtc,
            CreatedAtUtc: invite.CreatedAtUtc);

        return ApplicationResponse<CreateGuildInviteResponse>.Ok(payload);
    }
}
