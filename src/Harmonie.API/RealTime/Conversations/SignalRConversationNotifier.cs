using Harmonie.API.RealTime.Common;
using Harmonie.Application.Features.Conversations;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Conversations;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Conversations;

public sealed class SignalRConversationNotifier : IConversationNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRConversationNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyConversationCreatedAsync(
        ConversationCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ConversationCreatedEvent(
            ConversationId: notification.ConversationId.Value,
            Name: notification.Name,
            Participants: notification.Participants
                .Select(p => new ConversationParticipantEventDto(
                    UserId: p.UserId,
                    Username: p.Username,
                    DisplayName: p.DisplayName,
                    AvatarFileId: p.AvatarFileId,
                    Avatar: p.Avatar is not null
                        ? new AvatarAppearanceDto(p.Avatar.Color, p.Avatar.Icon, p.Avatar.Bg)
                        : null))
                .ToArray());

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ConversationCreated(payload, cancellationToken);
    }

    public async Task NotifyParticipantLeftAsync(
        ConversationParticipantLeftNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ConversationParticipantLeftEvent(
            ConversationId: notification.ConversationId.Value,
            UserId: notification.UserId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ConversationParticipantLeft(payload, cancellationToken);
    }
}

public sealed record ConversationCreatedEvent(
    Guid ConversationId,
    string? Name,
    IReadOnlyList<ConversationParticipantEventDto> Participants);

public sealed record ConversationParticipantEventDto(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar);

public sealed record ConversationParticipantLeftEvent(
    Guid ConversationId,
    Guid UserId);
