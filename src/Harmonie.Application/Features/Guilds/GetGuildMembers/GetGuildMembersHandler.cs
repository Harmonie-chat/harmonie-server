using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public sealed class GetGuildMembersHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILogger<GetGuildMembersHandler> _logger;

    public GetGuildMembersHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        ILogger<GetGuildMembersHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetGuildMembersResponse>> HandleAsync(
        GuildId guildId,
        UserId requesterUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetGuildMembers started. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
            guildId,
            requesterUserId);

        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            _logger.LogWarning(
                "GetGuildMembers guild not found. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            guildId,
            requesterUserId,
            cancellationToken);
        if (!isMember)
        {
            _logger.LogWarning(
                "GetGuildMembers access denied. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var members = await _guildMemberRepository.GetGuildMembersAsync(guildId, cancellationToken);

        _logger.LogInformation(
            "GetGuildMembers succeeded. GuildId={GuildId}, RequesterUserId={RequesterUserId}, MemberCount={MemberCount}",
            guildId,
            requesterUserId,
            members.Count);

        var payload = new GetGuildMembersResponse(
            GuildId: guildId.ToString(),
            Members: members.Select(member => new GetGuildMembersItemResponse(
                    UserId: member.UserId.ToString(),
                    Username: member.Username.Value,
                    DisplayName: member.DisplayName,
                    AvatarUrl: member.AvatarUrl,
                    IsActive: member.IsActive,
                    Role: member.Role.ToString(),
                    JoinedAtUtc: member.JoinedAtUtc))
                .ToArray());

        return ApplicationResponse<GetGuildMembersResponse>.Ok(payload);
    }
}
