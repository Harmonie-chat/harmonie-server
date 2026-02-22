# Guild Text Real-Time Plan (SignalR)

## Why SignalR

REST endpoints are sufficient for persistence and fetch, but chat UX needs low-latency propagation for new messages across clients.

SignalR is planned to provide server push while REST remains source of truth.

## Scope for Real-Time Phase

- push new text messages to connected channel members
- join and leave text channel groups
- enforce auth and membership before joining groups

Not in scope for initial real-time phase:

- message edit/delete push
- typing indicators
- online presence
- read receipts

## Proposed Hub

- Hub route: `/hubs/text-channels`
- Hub name: `TextChannelsHub`

Client to server methods:

- `JoinChannel(Guid channelId)`
- `LeaveChannel(Guid channelId)`

Server to client events:

- `MessageCreated(ChannelMessageDto message)`

## Security Model

- JWT bearer authentication required for hub connection.
- On `JoinChannel`, verify:
  - channel exists
  - channel type is `Text`
  - caller is member of channel guild
- Reject unauthorized joins with hub exception payload.

## Integration Boundary

Introduce an application-side notifier interface:

- `ITextChannelNotifier`
  - `Task NotifyMessageCreatedAsync(ChannelMessageDto message, CancellationToken cancellationToken)`

Behavior:

- message handler persists message first
- on persistence success, invoke notifier
- SignalR implementation publishes to group `channel:{channelId}`

This keeps domain/application independent from SignalR framework details.

## Ordering and Reliability

- Persist first, publish second.
- If publish fails, API call still succeeds because data is committed.
- Clients must support fallback polling via `GET /api/channels/{channelId}/messages`.

## Delivery Plan

1. Implement REST message flows first.
2. Add hub and notifier interface implementation.
3. Add integration tests for join authorization and event delivery.
4. Add resiliency logging and metrics.

## Testing Strategy

- Unit tests:
  - membership check before join
  - notifier called only after successful message save
- Integration tests:
  - two authenticated clients join same channel
  - client A sends message via REST
  - client B receives `MessageCreated`
  - unauthorized client cannot join and receives error
