using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.JoinVoiceChannel;

public sealed class JoinVoiceChannelHandler
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILiveKitTokenService _liveKitTokenService;
    private readonly ILogger<JoinVoiceChannelHandler> _logger;

    public JoinVoiceChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserRepository userRepository,
        ILiveKitTokenService liveKitTokenService,
        ILogger<JoinVoiceChannelHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _userRepository = userRepository;
        _liveKitTokenService = liveKitTokenService;
        _logger = logger;
    }

    public async Task<ApplicationResponse<JoinVoiceChannelResponse>> HandleAsync(
        GuildChannelId channelId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "JoinVoiceChannel started. ChannelId={ChannelId}, UserId={UserId}",
            channelId,
            currentUserId);

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            _logger.LogWarning(
                "JoinVoiceChannel failed because channel was not found. ChannelId={ChannelId}, UserId={UserId}",
                channelId,
                currentUserId);

            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Voice)
        {
            _logger.LogWarning(
                "JoinVoiceChannel failed because channel is not voice. ChannelId={ChannelId}, ChannelType={ChannelType}, UserId={UserId}",
                channelId,
                channel.Type,
                currentUserId);

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
            _logger.LogWarning(
                "JoinVoiceChannel access denied. ChannelId={ChannelId}, GuildId={GuildId}, UserId={UserId}",
                channelId,
                channel.GuildId,
                currentUserId);

            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "JoinVoiceChannel failed because user was not found. ChannelId={ChannelId}, UserId={UserId}",
                channelId,
                currentUserId);

            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User profile was not found");
        }

        var roomToken = await _liveKitTokenService.GenerateRoomTokenAsync(
            channelId,
            currentUserId,
            user.Username.Value,
            cancellationToken);

        _logger.LogInformation(
            "JoinVoiceChannel succeeded. ChannelId={ChannelId}, UserId={UserId}, RoomName={RoomName}",
            channelId,
            currentUserId,
            roomToken.RoomName);

        var payload = new JoinVoiceChannelResponse(
            Token: roomToken.Token,
            Url: roomToken.Url,
            RoomName: roomToken.RoomName);

        return ApplicationResponse<JoinVoiceChannelResponse>.Ok(payload);
    }
}
