# Getting Started

This guide gets the Harmonie server running locally.

## Prerequisites

- .NET 10 SDK
- Podman with `podman compose`

## 1. Start Local Services

```bash
podman compose up -d postgres livekit
```

Optional checks:

```bash
podman compose ps
podman compose logs postgres
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
- `LiveKit:RequestTimeoutSeconds` controls the timeout applied to outbound LiveKit HTTP calls. The development default is `5`.

When you run through `docker-compose`, the API container uses `http://livekit:7880` internally while clients still use `ws://localhost:7880`.

CORS allowed origins are configured through `Cors:AllowedOrigins`.
In `Development`, the default is `["*"]`.
For non-development environments, set explicit origins in configuration or environment variables such as `Cors__AllowedOrigins__0=https://app.example.com`.

Uploads are stored on the local filesystem and served by the API from `/files/*`.
In `docker-compose`, the API stores them in the `uploads-data` volume.

## 4. Verify the API

- Health: `GET /health` (checks both PostgreSQL and LiveKit reachability)
- Register: `POST /api/auth/register`
- Login: `POST /api/auth/login`
- Logout current session: `POST /api/auth/logout`
- Logout all sessions: `POST /api/auth/logout-all`
- Refresh token: `POST /api/auth/refresh`
- Create guild: `POST /api/guilds`
- List guilds: `GET /api/guilds`
- Update guild: `PATCH /api/guilds/{guildId}`
- List guild channels: `GET /api/guilds/{guildId}/channels`
- Open direct conversation: `POST /api/conversations`
- List direct conversations: `GET /api/conversations`
- Read conversation messages: `GET /api/conversations/{conversationId}/messages`
- Edit conversation message: `PATCH /api/conversations/{conversationId}/messages/{messageId}`
- Delete conversation message: `DELETE /api/conversations/{conversationId}/messages/{messageId}`
- Send conversation message: `POST /api/conversations/{conversationId}/messages`
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

The upload tests use the local filesystem provider. No external object storage service is required.

## Notes

- OpenAPI and Scalar API reference are enabled only in Development.
- Refresh tokens are persisted and rotated in storage.
- Message posting is rate-limited (`40` messages/minute per authenticated user).
- If the API cannot connect to PostgreSQL, confirm `podman compose` is running and port `5432` is available.
- If the voice snapshot tests fail, confirm LiveKit is running on port `7880`.
- If you later need multi-node or externalized file storage, an S3-compatible backend can be introduced behind `IObjectStorageService`.
