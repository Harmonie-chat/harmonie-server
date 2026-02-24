# Harmonie Server

Open-source, self-hosted communication platform backend.

## Current Scope

This repository currently provides:
- User registration and login endpoints
- JWT access token generation
- Refresh token persistence and rotation
- PostgreSQL persistence with Dapper
- DbUp migrations
- Unit and integration tests

## Tech Stack

- .NET 10 (Minimal APIs)
- PostgreSQL 18
- Dapper
- FluentValidation
- Serilog
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
- `POST /api/auth/refresh`
- `GET /api/guilds`

In Development, OpenAPI and Scalar are enabled.

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

## API Response Model

Auth endpoints return:
- Success: the feature response DTO (`RegisterResponse`, `LoginResponse`, `RefreshTokenResponse`)
- Error: a standardized `ApplicationError` payload (`code`, `message`, `details`)

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

## Project Structure

```text
src/
  Harmonie.API/              # Startup, middleware, HTTP pipeline
  Harmonie.Application/      # Vertical slices (feature endpoints/handlers/validators)
  Harmonie.Domain/           # Entities, value objects, domain rules
  Harmonie.Infrastructure/   # Dapper repository, JWT service, hashing
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
```

## Documentation

- `docs/GETTING_STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/VERTICAL_SLICE_ARCHITECTURE.md`
- `docs/features/guilds/README.md` (guild feature planning package)
- `agent.md` (AI assistant context)
- `CONTRIBUTING.md`

## License

AGPL-3.0. See `LICENSE`.
