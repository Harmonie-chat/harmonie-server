using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed class CreateGuildInviteHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGuildInviteHandler> _logger;

    public CreateGuildInviteHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateGuildInviteHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateGuildInviteResponse>> HandleAsync(
        GuildId guildId,
        CreateGuildInviteRequest request,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreateGuildInvite started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (guildAccess is null)
        {
            _logger.LogWarning(
                "CreateGuildInvite failed because guild was not found. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "CreateGuildInvite forbidden. GuildId={GuildId}, CallerId={CallerId}, CallerRole={CallerRole}",
                guildId,
                callerId,
                guildAccess.CallerRole);

            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can create invite links");
        }

        var inviteResult = GuildInvite.Create(guildId, callerId, request.MaxUses, request.ExpiresInHours);
        if (inviteResult.IsFailure || inviteResult.Value is null)
        {
            _logger.LogWarning(
                "CreateGuildInvite domain validation failed. GuildId={GuildId}, CallerId={CallerId}, Error={Error}",
                guildId,
                callerId,
                inviteResult.Error);

            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                inviteResult.Error ?? "Unable to create guild invite");
        }

        var invite = inviteResult.Value;

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildInviteRepository.AddAsync(invite, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "CreateGuildInvite succeeded. GuildId={GuildId}, CallerId={CallerId}, InviteId={InviteId}, Code={Code}",
            guildId,
            callerId,
            invite.Id,
            invite.Code);

        var payload = new CreateGuildInviteResponse(
            InviteId: invite.Id.ToString(),
            Code: invite.Code,
            GuildId: guildId.ToString(),
            CreatorId: callerId.ToString(),
            MaxUses: invite.MaxUses,
            UsesCount: invite.UsesCount,
            ExpiresAtUtc: invite.ExpiresAtUtc,
            CreatedAtUtc: invite.CreatedAtUtc);

        return ApplicationResponse<CreateGuildInviteResponse>.Ok(payload);
    }
}
