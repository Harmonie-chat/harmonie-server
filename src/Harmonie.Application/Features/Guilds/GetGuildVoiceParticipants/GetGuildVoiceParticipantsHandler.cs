using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;

public sealed class GetGuildVoiceParticipantsHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILiveKitRoomService _liveKitRoomService;
    private readonly ILogger<GetGuildVoiceParticipantsHandler> _logger;

    public GetGuildVoiceParticipantsHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        ILiveKitRoomService liveKitRoomService,
        ILogger<GetGuildVoiceParticipantsHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _liveKitRoomService = liveKitRoomService;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetGuildVoiceParticipantsResponse>> HandleAsync(
        GuildId guildId,
        UserId requesterUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetGuildVoiceParticipants started. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
            guildId,
            requesterUserId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, requesterUserId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "GetGuildVoiceParticipants guild not found. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "GetGuildVoiceParticipants access denied. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var channels = await _liveKitRoomService.GetGuildVoiceParticipantsAsync(guildId, cancellationToken);
        var members = await _guildMemberRepository.GetGuildMembersAsync(guildId, cancellationToken);
        var memberLookup = members.ToDictionary(m => m.UserId);

        _logger.LogInformation(
            "GetGuildVoiceParticipants succeeded. GuildId={GuildId}, RequesterUserId={RequesterUserId}, ActiveVoiceChannelCount={ActiveVoiceChannelCount}",
            guildId,
            requesterUserId,
            channels.Count);

        var payload = new GetGuildVoiceParticipantsResponse(
            channels.Select(channel => new GetGuildVoiceParticipantsChannelResponse(
                    ChannelId: channel.ChannelId.ToString(),
                    Participants: channel.Participants
                        .Select(participant =>
                        {
                            memberLookup.TryGetValue(participant.UserId, out var member);

                            var avatar = member is not null
                                         && (member.AvatarColor is not null || member.AvatarIcon is not null || member.AvatarBg is not null)
                                ? new AvatarAppearanceDto(member.AvatarColor, member.AvatarIcon, member.AvatarBg)
                                : null;

                            return new GetGuildVoiceParticipantResponse(
                                UserId: participant.UserId.ToString(),
                                Username: participant.Username,
                                DisplayName: member?.DisplayName,
                                AvatarFileId: member?.AvatarFileId?.ToString(),
                                Avatar: avatar);
                        })
                        .ToArray()))
                .ToArray());

        return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Ok(payload);
    }
}
