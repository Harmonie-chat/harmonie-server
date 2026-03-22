using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.JoinVoiceChannel;

public sealed class JoinVoiceChannelHandler : IAuthenticatedHandler<GuildChannelId, JoinVoiceChannelResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILiveKitTokenService _liveKitTokenService;

    public JoinVoiceChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserRepository userRepository,
        ILiveKitTokenService liveKitTokenService)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _userRepository = userRepository;
        _liveKitTokenService = liveKitTokenService;
    }

    public async Task<ApplicationResponse<JoinVoiceChannelResponse>> HandleAsync(
        GuildChannelId request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var channel = await _guildChannelRepository.GetByIdAsync(request, cancellationToken);
        if (channel is null)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Voice)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NotVoice,
                "Live voice can only be joined for voice channels");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            channel.GuildId,
            currentUserId,
            cancellationToken);
        if (!isMember)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User profile was not found");
        }

        var roomToken = await _liveKitTokenService.GenerateRoomTokenAsync(
            request,
            currentUserId,
            user.Username.Value,
            cancellationToken);

        var payload = new JoinVoiceChannelResponse(
            Token: roomToken.Token,
            Url: roomToken.Url,
            RoomName: roomToken.RoomName);

        return ApplicationResponse<JoinVoiceChannelResponse>.Ok(payload);
    }
}
