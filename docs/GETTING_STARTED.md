# Getting Started

This guide gets the Harmonie server running locally.

## Prerequisites

- .NET 10 SDK
- Docker (for PostgreSQL)

## 1. Start PostgreSQL

```bash
docker-compose up -d postgres
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
- Send message: `POST /api/channels/{channelId}/messages`
- Read messages: `GET /api/channels/{channelId}/messages`
- Get current user profile: `GET /api/users/me`

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
