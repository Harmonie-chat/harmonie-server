# Harmonie Server

Open-source, self-hosted communication platform backend.

## Current Scope

This repository currently provides:
- User registration and login endpoints
- JWT access token generation
- Refresh token generation (persistence/rotation is not implemented yet)
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

In Development, OpenAPI and Scalar are enabled.

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
- `agent.md` (AI assistant context)
- `CONTRIBUTING.md`

## License

AGPL-3.0. See `LICENSE`.
