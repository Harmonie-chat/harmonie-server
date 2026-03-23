using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public sealed class HandleLiveKitWebhookHandler : IHandler<HandleLiveKitWebhookRequest, HandleLiveKitWebhookResponse>
{
    private const string ParticipantJoinedEvent = "participant_joined";
    private const string ParticipantLeftEvent = "participant_left";
    private const string ChannelRoomPrefix = "channel:";

    private readonly ILiveKitWebhookReceiver _webhookReceiver;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IVoicePresenceNotifier _voicePresenceNotifier;

    public HandleLiveKitWebhookHandler(
        ILiveKitWebhookReceiver webhookReceiver,
        IGuildChannelRepository guildChannelRepository,
        IVoicePresenceNotifier voicePresenceNotifier)
    {
        _webhookReceiver = webhookReceiver;
        _guildChannelRepository = guildChannelRepository;
        _voicePresenceNotifier = voicePresenceNotifier;
    }

    public async Task<ApplicationResponse<HandleLiveKitWebhookResponse>> HandleAsync(
        HandleLiveKitWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var receiveResult = _webhookReceiver.Receive(request.RawBody, request.AuthorizationHeader ?? string.Empty);
        if (!receiveResult.Success)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                "LiveKit webhook signature is invalid.");
        }

        if (receiveResult.Event is not { } webhookEvent)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "LiveKit webhook receiver returned success without an event.");
        }

        var eventType = string.IsNullOrWhiteSpace(webhookEvent.EventType)
            ? "unknown"
            : webhookEvent.EventType;

        if (eventType is not ParticipantJoinedEvent and not ParticipantLeftEvent)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (!TryParseChannelId(webhookEvent.RoomName, out var channelId) || channelId is null)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (channel.Type != GuildChannelType.Voice)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (!TryParseUserId(webhookEvent.ParticipantIdentity, out var participantUserId) || participantUserId is null)
        {
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        var participantName = string.IsNullOrWhiteSpace(webhookEvent.ParticipantName)
            ? participantUserId.ToString()
            : webhookEvent.ParticipantName;

        if (eventType == ParticipantJoinedEvent)
        {
            await _voicePresenceNotifier.NotifyParticipantJoinedAsync(
                new VoiceParticipantJoinedNotification(
                    channel.GuildId,
                    channel.Id,
                    participantUserId,
                    participantName,
                    webhookEvent.OccurredAtUtc),
                cancellationToken);
        }
        else
        {
            await _voicePresenceNotifier.NotifyParticipantLeftAsync(
                new VoiceParticipantLeftNotification(
                    channel.GuildId,
                    channel.Id,
                    participantUserId,
                    participantName,
                    webhookEvent.OccurredAtUtc),
                cancellationToken);
        }

        return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(true, eventType));
    }

    private static bool TryParseChannelId(string? roomName, out GuildChannelId? channelId)
    {
        channelId = null;

        if (string.IsNullOrWhiteSpace(roomName)
            || !roomName.StartsWith(ChannelRoomPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rawChannelId = roomName[ChannelRoomPrefix.Length..];
        if (!GuildChannelId.TryParse(rawChannelId, out channelId) || channelId is null)
            return false;

        return true;
    }

    private static bool TryParseUserId(string? participantIdentity, out UserId? userId)
    {
        userId = null;

        if (string.IsNullOrWhiteSpace(participantIdentity))
            return false;

        if (!UserId.TryParse(participantIdentity, out userId) || userId is null)
            return false;

        return true;
    }
}
