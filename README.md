# Harmonie Server

Open-source, self-hosted communication platform backend.

## Tech Stack

- .NET 10 (Minimal APIs)
- PostgreSQL 18
- Dapper
- FluentValidation
- Serilog
- SignalR
- LiveKit
- OpenAPI + Scalar API reference

## Getting Started

See `docs/GETTING_STARTED.md` for setup instructions.

## Agent Dev Container

All agent-specific tooling is grouped under `agents/`.

1. Build the agent image:

```bash
podman build -f agents/Dockerfile.codex -t harmonie-codex .
```

2. Start an interactive shell with the repository mounted:

PowerShell:

```powershell
podman run --rm -it `
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
  MVP/
```

## Documentation

- `docs/GETTING_STARTED.md` — setup and local development
- `docs/ARCHITECTURE.md` — layers, request flow, cross-cutting concerns
- `CLAUDE.md` — AI agent instructions and conventions

## Scalability TODOs

### Critical (required for multi-instance)

- **SignalR backplane**: real-time delivery only works on a single instance today. Add a Redis or Azure SignalR backplane so messages are broadcast across all instances.

### Observability

- **Health check details**: `GET /health` reports status only. Add DB connectivity and (future) Redis checks so orchestrators can detect partial failures.
- **Metrics**: no Prometheus/OpenTelemetry integration. Add instrumentation for request latency, DB query duration, and SignalR connection count.

## License

AGPL-3.0. See `LICENSE`.
