# Harmonie Server

Open-source, self-hosted communication platform backend.

## Current Scope

This repository currently provides:
- User registration, login, refresh token rotation, session logout, and logout-all session revocation
- Refresh token persistence in PostgreSQL
- Refresh token reuse detection with family session revocation on security incident
- Guild creation and membership management (invite + list members)
- Guild channel listing with default text and voice channels
- Text messaging (send + read with cursor-based pagination)
- SignalR real-time delivery for text channel messages
- Rate limiting for message posting
- Unit and integration tests for auth, guild flows, messaging, and real-time delivery

## Tech Stack

- .NET 10 (Minimal APIs)
- PostgreSQL 18
- Dapper
- FluentValidation
- Serilog
- SignalR
- OpenAPI + Scalar API reference

## Quick Start

1. Start PostgreSQL:

```bash
docker-compose up -d postgres
```

2. Run migrations:

```bash
dotnet run --project tools/Harmonie.Migrations
```

3. Run API in Development:

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Harmonie.API
```

4. Check endpoints:
- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`
- `POST /api/auth/refresh`
- `POST /api/guilds`
- `GET /api/guilds`
- `POST /api/guilds/{guildId}/members/invite`
- `GET /api/guilds/{guildId}/members`
- `GET /api/guilds/{guildId}/channels`
- `POST /api/channels/{channelId}/messages`
- `GET /api/channels/{channelId}/messages`
- `GET /api/users/me`
- `PUT /api/users/me`
- `GET /hubs/text-channels` (SignalR negotiate/transport)

In Development, OpenAPI and Scalar are enabled.

## API Response Model

Endpoints return:
- Success: feature response DTOs
- Error: standardized `ApplicationError` payload (`code`, `message`, `details`)

Success example:

```json
{
  "userId": "d8f2a3d1-3f27-4f8b-8f42-7b79f12ad7b7",
  "email": "user@harmonie.chat",
  "username": "user123",
  "accessToken": "eyJ...",
  "refreshToken": "vL...",
  "expiresAt": "2026-02-22T12:00:00Z"
}
```

Error example:

```json
{
  "code": "AUTH_INVALID_CREDENTIALS",
  "message": "Invalid email/username or password",
  "details": null
}
```

Refresh-token security incidents return stable code `AUTH_REFRESH_TOKEN_REUSE_DETECTED` with HTTP `401`.

## Agent Dev Container

All agent-specific tooling is grouped under `agents/`.

1. Build the agent image:

```bash
docker build -f agents/Dockerfile.codex -t harmonie-codex .
```

2. Start an interactive shell with the repository mounted:

PowerShell:

```powershell
docker run --rm -it `
  --entrypoint bash `
  -v "${PWD}:/workspace" `
  -v "${env:USERPROFILE}\.codex:/root/.codex" `
  -e OPENAI_API_KEY="${env:OPENAI_API_KEY}" `
  harmonie-codex
```

3. Inside the container, run Codex manually:

```bash
cd /workspace
codex
```

4. Optional: run CI-like setup inside the container (PostgreSQL + migrations + build + tests):

```bash
bash /workspace/agents/setup-inside-codex.sh
```

## Project Structure

```text
src/
  Harmonie.API/              # Startup, middleware, HTTP pipeline, SignalR hub
  Harmonie.Application/      # Vertical slices (feature endpoints/handlers/validators)
  Harmonie.Domain/           # Entities, value objects, domain rules
  Harmonie.Infrastructure/   # Dapper repositories, JWT service, hashing
tests/
  Harmonie.Domain.Tests/
  Harmonie.Application.Tests/
  Harmonie.API.IntegrationTests/
tools/
  Harmonie.Migrations/       # DbUp migration runner + SQL scripts
docs/
  ARCHITECTURE.md
  GETTING_STARTED.md
  VERTICAL_SLICE_ARCHITECTURE.md
  MVP/
```

## Documentation

- `docs/GETTING_STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/VERTICAL_SLICE_ARCHITECTURE.md`
- `docs/MVP/README.md` (backlog-ready MVP tickets)
- `docs/features/guilds/README.md` (implementation design package)
- `AGENTS.md` (AI assistant context)
- `CONTRIBUTING.md`

## Scalability TODO

Known limitations and improvements needed before horizontal scaling or high-load deployments.

### Critical (required for multi-instance)

- **SignalR backplane**: real-time delivery only works on a single instance today. Add a Redis or Azure SignalR backplane so messages are broadcast across all instances.
- **Distributed rate limiting**: the `message-post` rate limiter is in-memory per instance. Move to a Redis-based distributed limiter so limits are enforced across instances.

### High impact (single instance, 500–1000 users)

- **Database connection pool tuning**: Npgsql defaults to `MaxPoolSize=30`. Explicitly set `MaxPoolSize=100;MinPoolSize=10` in the connection string to avoid exhaustion under load.
- **Cache layer**: no caching exists today. Guild channels, guild members, and user profiles are stable data queried on every request. Adding Redis cache with short TTLs (5–10 min) and mutation-triggered invalidation would significantly reduce DB load.

### Security / hardening

- **Rate limit auth endpoints**: `POST /api/auth/login`, `POST /api/auth/register`, and `POST /api/auth/refresh` are not rate-limited. Add per-IP limits to prevent brute-force and token-farming attacks.
- **CORS policy**: `AllowAnyOrigin` is acceptable for development but must be restricted to known origins in production.
- **SignalR JWT via query parameter**: the `?access_token=` transport is logged by proxies and visible in browser history. Prefer an httpOnly cookie for the SignalR connection once a client-side session model is in place.

### Observability

- **Health check details**: `GET /health` reports status only. Add DB connectivity and (future) Redis checks so orchestrators can detect partial failures.
- **Metrics**: no Prometheus/OpenTelemetry integration. Add instrumentation for request latency, DB query duration, and SignalR connection count.

## License

AGPL-3.0. See `LICENSE`.
