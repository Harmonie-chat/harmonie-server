# Guild Feature Planning

This folder contains the full planning package for the Guild feature before implementation.

## Product Goal

Deliver the first collaborative workflow:

1. User A creates a guild.
2. User A is automatically guild administrator.
3. User A invites User B, who becomes guild member.
4. Both users can see the default text channel.
5. User A sends a message in the default text channel.
6. User B can read that message.

Voice channel entities are prepared at data level, but voice behavior is out of scope for the first implementation cycle.

## Documents

- `docs/features/guilds/GUILD_TEXT_SPEC.md`: business rules and feature scope.
- `docs/features/guilds/GUILD_TEXT_DATA_MODEL.md`: domain model and PostgreSQL schema plan.
- `docs/features/guilds/GUILD_TEXT_API_CONTRACT.md`: REST contract and error code catalog.
- `docs/features/guilds/GUILD_TEXT_SIGNALR_PLAN.md`: real-time messaging strategy with SignalR.
- `docs/features/guilds/GUILD_TEXT_IMPLEMENTATION_ROADMAP.md`: phased delivery plan, acceptance criteria, and testing matrix.

## Scope Policy

- This planning package is intentionally implementation-first and concrete.
- All contracts use current project conventions:
  - `ApplicationResponse<T>` for internal handler flow.
  - success HTTP responses return feature DTOs directly.
  - error HTTP responses return `ApplicationError`.
