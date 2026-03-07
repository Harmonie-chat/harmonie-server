using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public sealed class HandleLiveKitWebhookHandler
{
    private const string ParticipantJoinedEvent = "participant_joined";
    private const string ParticipantLeftEvent = "participant_left";
    private const string ChannelRoomPrefix = "channel:";

    private readonly ILiveKitWebhookReceiver _webhookReceiver;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IVoicePresenceNotifier _voicePresenceNotifier;
    private readonly ILogger<HandleLiveKitWebhookHandler> _logger;

    public HandleLiveKitWebhookHandler(
        ILiveKitWebhookReceiver webhookReceiver,
        IGuildChannelRepository guildChannelRepository,
        IVoicePresenceNotifier voicePresenceNotifier,
        ILogger<HandleLiveKitWebhookHandler> logger)
    {
        _webhookReceiver = webhookReceiver;
        _guildChannelRepository = guildChannelRepository;
        _voicePresenceNotifier = voicePresenceNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<HandleLiveKitWebhookResponse>> HandleAsync(
        HandleLiveKitWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var receiveResult = _webhookReceiver.Receive(request.RawBody, request.AuthorizationHeader ?? string.Empty);
        if (!receiveResult.Success)
        {
            _logger.LogWarning(
                "LiveKit webhook rejected because signature validation failed. Detail={Detail}",
                receiveResult.ErrorDetail);

            return ApplicationResponse<HandleLiveKitWebhookResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                "LiveKit webhook signature is invalid.");
        }

        if (receiveResult.Event is not { } webhookEvent)
        {
            _logger.LogError("LiveKit webhook receiver returned success without an event payload.");

            return ApplicationResponse<HandleLiveKitWebhookResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "LiveKit webhook receiver returned success without an event.");
        }

        var eventType = string.IsNullOrWhiteSpace(webhookEvent.EventType)
            ? "unknown"
            : webhookEvent.EventType;

        if (eventType is not ParticipantJoinedEvent and not ParticipantLeftEvent)
        {
            _logger.LogDebug("LiveKit webhook ignored because event type is not supported. EventType={EventType}", eventType);
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (!TryParseChannelId(webhookEvent.RoomName, out var channelId) || channelId is null)
        {
            _logger.LogWarning(
                "LiveKit webhook ignored because room name does not match channel convention. EventType={EventType}, RoomName={RoomName}",
                eventType,
                webhookEvent.RoomName);

            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            _logger.LogWarning(
                "LiveKit webhook ignored because channel was not found. EventType={EventType}, ChannelId={ChannelId}",
                eventType,
                channelId);

            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (channel.Type != GuildChannelType.Voice)
        {
            _logger.LogWarning(
                "LiveKit webhook ignored because resolved channel is not voice. EventType={EventType}, ChannelId={ChannelId}, ChannelType={ChannelType}",
                eventType,
                channelId,
                channel.Type);

            return ApplicationResponse<HandleLiveKitWebhookResponse>.Ok(new(false, eventType));
        }

        if (!TryParseUserId(webhookEvent.ParticipantIdentity, out var participantUserId) || participantUserId is null)
        {
            _logger.LogWarning(
                "LiveKit webhook ignored because participant identity is invalid. EventType={EventType}, ParticipantIdentity={ParticipantIdentity}",
                eventType,
                webhookEvent.ParticipantIdentity);

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

        _logger.LogInformation(
            "LiveKit webhook processed. EventType={EventType}, GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId}",
            eventType,
            channel.GuildId,
            channel.Id,
            participantUserId);

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
