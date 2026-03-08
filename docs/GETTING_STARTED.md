# Getting Started

This guide gets the Harmonie server running locally.

## Prerequisites

- .NET 10 SDK
- Docker (for PostgreSQL)

## 1. Start PostgreSQL

```bash
docker-compose up -d postgres livekit
```

Optional checks:

```bash
docker-compose ps
docker-compose logs postgres
```

## 2. Apply Database Migrations

```bash
dotnet run --project tools/Harmonie.Migrations
```

The migration runner executes embedded SQL scripts from `tools/Harmonie.Migrations/Scripts`.

## 3. Run the API

Set Development environment (required for local connection string and JWT settings in `appsettings.Development.json`).

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Harmonie.API
```

LiveKit defaults are split on purpose:
- `LiveKit:PublicUrl` is the client-facing WebSocket URL returned by the API.
- `LiveKit:InternalUrl` is the HTTP base URL used by the server SDK for room queries.

When you run through `docker-compose`, the API container uses `http://livekit:7880` internally while clients still use `ws://localhost:7880`.

## 4. Verify the API

- Health: `GET /health`
- Register: `POST /api/auth/register`
- Login: `POST /api/auth/login`
- Logout current session: `POST /api/auth/logout`
- Logout all sessions: `POST /api/auth/logout-all`
- Refresh token: `POST /api/auth/refresh`
- Create guild: `POST /api/guilds`
- List guilds: `GET /api/guilds`
- List guild channels: `GET /api/guilds/{guildId}/channels`
- Open direct conversation: `POST /api/conversations`
- List direct conversations: `GET /api/conversations`
- Read direct messages: `GET /api/conversations/{conversationId}/messages`
- Edit direct message: `PUT /api/conversations/{conversationId}/messages/{messageId}`
- Send direct message: `POST /api/conversations/{conversationId}/messages`
- Send message: `POST /api/channels/{channelId}/messages`
- Read messages: `GET /api/channels/{channelId}/messages`
- Get current user profile: `GET /api/users/me`
- Update current user profile: `PUT /api/users/me`

Example register payload:

```json
{
  "email": "test@harmonie.chat",
  "username": "testuser",
  "password": "Test123!@#"
}
```

## 5. Run Tests

```bash
dotnet test
```

## Notes

- OpenAPI and Scalar API reference are enabled only in Development.
- Refresh tokens are persisted and rotated in storage.
- Message posting is rate-limited (`40` messages/minute per authenticated user).
- If the API cannot connect to PostgreSQL, confirm `docker-compose` is running and port `5432` is available.
- If the voice snapshot tests fail, confirm LiveKit is running on port `7880`.
