# Architecture Overview

Harmonie follows a Clean Architecture direction with Vertical Slice organization in the Application layer.

## Layers

## API (`src/Harmonie.API`)

Responsibilities:
- Application startup and middleware pipeline
- JWT authentication and authorization wiring
- OpenAPI/Scalar setup (Development only)
- Endpoint mapping entry point

Current mapped endpoints:
- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`
- `POST /api/auth/refresh`
- `POST /api/guilds`
- `GET /api/guilds`
- `PATCH /api/guilds/{guildId}`
- `POST /api/guilds/{guildId}/members/invite`
- `GET /api/guilds/{guildId}/members`
- `GET /api/guilds/{guildId}/channels`
- `GET /api/guilds/{guildId}/voice/participants`
- `POST /api/channels/{channelId}/messages`
- `GET /api/channels/{channelId}/messages`
- `GET /api/conversations/{conversationId}/messages`
- `PATCH /api/conversations/{conversationId}/messages/{messageId}`
- `DELETE /api/conversations/{conversationId}/messages/{messageId}`
- `GET /api/users/me`
- `PUT /api/users/me`
- `GET /hubs/realtime` (SignalR for text channels and voice presence)

## Application (`src/Harmonie.Application`)

Responsibilities:
- Feature slices (request, validator, handler, endpoint)
- Orchestration of domain logic through interfaces
- Validation registration and handler DI registration

Current features:
- `Features/Auth/Register/*`
- `Features/Auth/Login/*`
- `Features/Auth/RefreshToken/*`
- `Features/Auth/Logout/*`
- `Features/Auth/LogoutAll/*`
- `Features/Guilds/CreateGuild/*`
- `Features/Guilds/ListUserGuilds/*`
- `Features/Guilds/UpdateGuild/*`
- `Features/Guilds/InviteMember/*`
- `Features/Guilds/GetGuildMembers/*`
- `Features/Guilds/GetGuildChannels/*`
- `Features/Channels/SendMessage/*`
- `Features/Channels/GetMessages/*`
- `Features/Users/GetMyProfile/*`
- `Features/Users/UpdateMyProfile/*`

Shared:
- `Common/IEndpoint.cs`
- `Common/EndpointExtensions.cs`
- `Interfaces/*` ports for repository/hash/token services

## Domain (`src/Harmonie.Domain`)

Responsibilities:
- Business model and invariants
- Entities (`User`, `Guild`, `GuildMember`, `GuildChannel`, `ChannelMessage`)
- Value objects (`Email`, `Username`, `UserId`, `GuildId`, `GuildName`, `GuildChannelId`, `ChannelMessageId`, `ChannelMessageContent`)
- Domain exceptions and events
- Result pattern (`Common/Result.cs`)

This layer has no dependency on application/infrastructure/web concerns.

## Infrastructure (`src/Harmonie.Infrastructure`)

Responsibilities:
- Adapter implementations for application interfaces
- Dapper-based repositories (`UserRepository`, `GuildRepository`, `GuildMemberRepository`, `GuildChannelRepository`, `ChannelMessageRepository`, `RefreshTokenRepository`)
- JWT generation/validation and password hashing
- Options/configuration objects

## Data and Migration Strategy

- DB: PostgreSQL
- Access: Dapper + Npgsql
- Migrations: DbUp runner in `tools/Harmonie.Migrations`
- Core schema scripts:
  - `tools/Harmonie.Migrations/Scripts/20260215_01_CreateUsersTable.sql`
  - `tools/Harmonie.Migrations/Scripts/20260222_02_CreateRefreshTokensTable.sql`
  - `tools/Harmonie.Migrations/Scripts/20260222_03_CreateGuildsTable.sql`
  - `tools/Harmonie.Migrations/Scripts/20260222_04_CreateGuildMembersTable.sql`
  - `tools/Harmonie.Migrations/Scripts/20260222_05_CreateGuildChannelsTable.sql`
  - `tools/Harmonie.Migrations/Scripts/20260222_06_CreateChannelMessagesTable.sql`

## Request Flow (Example: Send Message)

1. Endpoint receives request (`SendMessageEndpoint`)
2. FluentValidation validates request DTO
3. Handler loads channel and membership permissions
4. Domain value objects/entities enforce message rules
5. Repository persists message in a transaction
6. SignalR notifier emits best-effort `MessageCreated` event

## Cross-Cutting Concerns

- Standardized internal application result flow: `ApplicationResponse<T>` in handlers
- Success HTTP responses return feature DTOs directly
- Standardized error contract: `ApplicationError` (`code`, `message`, `details`)
- Global exception handling (unexpected errors fallback): `src/Harmonie.API/Middleware/GlobalExceptionHandler.cs`
- Structured logging: Serilog
- Authentication: JWT bearer middleware
- Authorization: membership checks in handlers and SignalR hub methods
- Throttling: fixed-window limiter for message posting (`message-post` policy)

## Known Gaps

- Guild lifecycle completion (leave, kick, role changes, owner transfer)
- Channel lifecycle management (create/rename/reorder/delete)
- Message lifecycle management (edit/delete)
- MVP backlog tickets: `docs/MVP/README.md`
