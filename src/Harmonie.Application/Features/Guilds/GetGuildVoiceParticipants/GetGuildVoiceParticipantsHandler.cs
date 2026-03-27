using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;

public sealed class GetGuildVoiceParticipantsHandler : IAuthenticatedHandler<GuildId, GetGuildVoiceParticipantsResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILiveKitRoomService _liveKitRoomService;

    public GetGuildVoiceParticipantsHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        ILiveKitRoomService liveKitRoomService)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _liveKitRoomService = liveKitRoomService;
    }

    public async Task<ApplicationResponse<GetGuildVoiceParticipantsResponse>> HandleAsync(
        GuildId guildId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var channels = await _liveKitRoomService.GetGuildVoiceParticipantsAsync(guildId, cancellationToken);
        var members = await _guildMemberRepository.GetGuildMembersAsync(guildId, cancellationToken);
        var memberLookup = members.ToDictionary(m => m.UserId);

        var payload = new GetGuildVoiceParticipantsResponse(
            channels.Select(channel => new GetGuildVoiceParticipantsChannelResponse(
                    ChannelId: channel.ChannelId.Value,
                    Participants: channel.Participants
                        .Select(participant =>
                        {
                            memberLookup.TryGetValue(participant.UserId, out var member);

                            var avatar = member is not null
                                         && (member.AvatarColor is not null || member.AvatarIcon is not null || member.AvatarBg is not null)
                                ? new AvatarAppearanceDto(member.AvatarColor, member.AvatarIcon, member.AvatarBg)
                                : null;

                            return new GetGuildVoiceParticipantResponse(
                                UserId: participant.UserId.Value,
                                Username: participant.Username,
                                DisplayName: member?.DisplayName,
                                AvatarFileId: member?.AvatarFileId?.Value,
                                Avatar: avatar);
                        })
                        .ToArray()))
                .ToArray());

        return ApplicationResponse<GetGuildVoiceParticipantsResponse>.Ok(payload);
    }
}
