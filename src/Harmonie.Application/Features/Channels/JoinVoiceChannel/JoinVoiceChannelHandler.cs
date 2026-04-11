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
    private readonly ILiveKitRoomService _liveKitRoomService;
    private readonly IVoiceParticipantCache _voiceParticipantCache;

    public JoinVoiceChannelHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserRepository userRepository,
        ILiveKitTokenService liveKitTokenService,
        ILiveKitRoomService liveKitRoomService,
        IVoiceParticipantCache voiceParticipantCache)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _userRepository = userRepository;
        _liveKitTokenService = liveKitTokenService;
        _liveKitRoomService = liveKitRoomService;
        _voiceParticipantCache = voiceParticipantCache;
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

        var roomTokenTask = _liveKitTokenService.GenerateRoomTokenAsync(
            request,
            currentUserId,
            user.Username.Value,
            cancellationToken);
        var liveKitParticipantsTask = _liveKitRoomService.ListChannelParticipantsAsync(request, cancellationToken);
        var cachedParticipantsTask = _voiceParticipantCache.GetAsync(request, cancellationToken);

        await Task.WhenAll(roomTokenTask, liveKitParticipantsTask, cachedParticipantsTask);

        var roomToken = roomTokenTask.Result;
        var liveKitParticipants = liveKitParticipantsTask.Result;
        var cachedParticipants = cachedParticipantsTask.Result;

        var cachedById = cachedParticipants.ToDictionary(p => p.UserId.Value);
        var liveKitIds = liveKitParticipants.Select(p => p.UserId.Value).ToHashSet();

        var missingIds = liveKitParticipants
            .Where(p => !cachedById.ContainsKey(p.UserId.Value))
            .Select(p => p.UserId)
            .ToArray();

        var fetchedUsers = missingIds.Length > 0
            ? await _userRepository.GetManyByIdsAsync(missingIds, cancellationToken)
            : [];
        var fetchedById = fetchedUsers.ToDictionary(u => u.Id.Value);

        var reconciledParticipants = new List<CachedVoiceParticipant>(liveKitParticipants.Count);

        foreach (var lkParticipant in liveKitParticipants)
        {
            CachedVoiceParticipant cached;

            if (cachedById.TryGetValue(lkParticipant.UserId.Value, out var existing))
            {
                cached = existing;
            }
            else if (fetchedById.TryGetValue(lkParticipant.UserId.Value, out var dbUser))
            {
                cached = new CachedVoiceParticipant(
                    UserId: dbUser.Id,
                    Username: dbUser.Username.Value,
                    DisplayName: dbUser.DisplayName,
                    AvatarFileId: dbUser.AvatarFileId,
                    AvatarColor: dbUser.AvatarColor,
                    AvatarIcon: dbUser.AvatarIcon,
                    AvatarBg: dbUser.AvatarBg);
            }
            else
            {
                cached = new CachedVoiceParticipant(
                    UserId: lkParticipant.UserId,
                    Username: lkParticipant.Username,
                    DisplayName: null,
                    AvatarFileId: null,
                    AvatarColor: null,
                    AvatarIcon: null,
                    AvatarBg: null);
            }

            await _voiceParticipantCache.AddOrUpdateAsync(request, cached, cancellationToken);
            reconciledParticipants.Add(cached);
        }

        foreach (var stale in cachedParticipants.Where(p => !liveKitIds.Contains(p.UserId.Value)))
            await _voiceParticipantCache.RemoveAsync(request, stale.UserId, cancellationToken);

        var currentParticipants = reconciledParticipants
            .Select(p => new JoinVoiceChannelParticipantResponse(
                UserId: p.UserId.Value,
                Username: p.Username,
                DisplayName: p.DisplayName,
                AvatarFileId: p.AvatarFileId?.Value,
                AvatarColor: p.AvatarColor,
                AvatarIcon: p.AvatarIcon,
                AvatarBg: p.AvatarBg))
            .ToArray();

        var payload = new JoinVoiceChannelResponse(
            Token: roomToken.Token,
            Url: roomToken.Url,
            RoomName: roomToken.RoomName,
            CurrentParticipants: currentParticipants);

        return ApplicationResponse<JoinVoiceChannelResponse>.Ok(payload);
    }
}
