# Guild Text Implementation Roadmap

## Delivery Strategy

Implement in vertical slices with strict passing tests at each phase.

## Phase 0: Documentation and Contract Freeze

- finalize scope and business rules
- finalize data model
- finalize API contracts
- finalize error code catalog
- finalize real-time strategy

Exit criteria:

- this planning package is reviewed and accepted

## Phase 1: Guild and Membership Core

Goal:

- create guild
- assign owner admin
- invite member
- list channels

Work items:

- Domain:
  - add guild entities/value objects and role/channel enums
- Application interfaces:
  - add repositories for guild, member, channel
- Application slices:
  - `Features/Guilds/CreateGuild`
  - `Features/Guilds/InviteMember`
  - `Features/Guilds/GetGuildChannels`
- Infrastructure:
  - Dapper repositories for guild/member/channel
  - transactional guild creation with default channels
- API:
  - map endpoints and OpenAPI metadata
  - enforce JWT auth

Exit criteria:

- User A can create guild and invite User B
- User B can list channels and see default text channel

## Phase 2: Text Messages via REST

Goal:

- send and read text messages from channel endpoints

Work items:

- Domain:
  - add message entity/value objects
- Application interfaces:
  - add message repository
- Application slices:
  - `Features/Channels/SendMessage`
  - `Features/Channels/GetMessages`
- Infrastructure:
  - Dapper message repository with cursor pagination
- API:
  - map message endpoints and OpenAPI metadata

Exit criteria:

- User A sends message in default text channel
- User B reads the same message through API

## Phase 3: Real-Time Messaging with SignalR

Goal:

- near real-time visibility of new messages across clients

Work items:

- add `TextChannelsHub`
- add `ITextChannelNotifier` abstraction in application
- add SignalR notifier implementation in infrastructure/api
- call notifier from send message flow after commit

Exit criteria:

- two authenticated clients connected to same channel receive push updates

## Phase 4: Hardening and Observability

Goal:

- improve reliability and operational visibility

Work items:

- structured logs around guild/message operations
- rate limit or abuse guardrails on message posting
- query tuning and index review for messages pagination
- integration test coverage expansion

Exit criteria:

- performance and reliability baseline documented

## Planned Error Codes

- Guild:
  - `GUILD_NOT_FOUND`
  - `GUILD_ACCESS_DENIED`
  - `GUILD_INVITE_FORBIDDEN`
  - `GUILD_INVITE_TARGET_NOT_FOUND`
  - `GUILD_MEMBER_ALREADY_EXISTS`
- Channel:
  - `CHANNEL_NOT_FOUND`
  - `CHANNEL_NOT_TEXT`
  - `CHANNEL_ACCESS_DENIED`
- Message:
  - `MESSAGE_CONTENT_EMPTY`
  - `MESSAGE_CONTENT_TOO_LONG`

## Testing Matrix

- Domain tests:
  - guild name validation
  - role constraints
  - message content constraints
- Application tests:
  - invite authorization
  - duplicate member conflict
  - message authorization
  - message pagination behavior
- API integration tests:
  - complete happy path from guild creation to cross-user message read
  - forbidden and not found scenarios
  - validation error payload and error code assertions
- SignalR integration tests:
  - channel join authorization
  - `MessageCreated` delivery

## Primary End-to-End Acceptance Test

1. Register/login user A.
2. Register/login user B.
3. User A creates guild.
4. User A invites user B.
5. User B lists channels and sees default text channel.
6. User A posts message in default text channel.
7. User B fetches messages and sees the posted message.
8. SignalR phase: user B receives `MessageCreated` push without polling.

## Risk Register

- Risk: permission checks spread across layers and become inconsistent.
  - Mitigation: centralize membership checks in dedicated query helpers and application policies.
- Risk: pagination bugs with duplicate timestamps.
  - Mitigation: cursor with `(created_at_utc, id)` tie-breaker.
- Risk: SignalR push failures.
  - Mitigation: REST remains source of truth, client retry polling fallback.
