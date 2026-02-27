# AGENTS Rules - Harmonie Server

Scope: canonical instructions for AI coding agents working in this repository.

## Mandatory Rules

1. Nullable safety is required:
- Any nullable path must be handled explicitly in code.
- Do not assume non-null from external boundaries (HTTP, config, DB, env, deserialization).
- Limit null checks to values that are actually nullable in type flow (`T?`, maybe-null analysis) or originate from external/untrusted boundaries.
- Do not add defensive null checks for non-nullable internal parameters that are guaranteed by compile-time contracts.
- Add or update tests to cover nullable paths when behavior changes.

2. Null-forgiving operator policy:
- In non-test code (`src/**`, `tools/**`), do not use `!` to silence nullable warnings.
- In test projects (`tests/**`), using `!` is allowed when it improves test readability.
- If a value can be null in type flow, prefer guards, pattern matching, validation, or explicit error handling.

3. Build must stay warning-clean for nullable correctness:
- Fix nullable warnings by code changes, not suppression shortcuts.

4. Feature alignment verification is mandatory:
- For any new endpoint/feature or behavior change, compare implementation against at least 2 existing features before finalizing.
- Match established conventions (endpoint flow, validator usage, handler error mapping, DI registration, Program mapping, and test style).
- If misaligned, update the new code to align unless there is a documented reason not to.

5. HTTP input nullability checks must live in FluentValidation:
- For HTTP request DTOs (body/query/route models), non-null/empty boundary checks must be defined in `*Validator.cs`.
- Do not duplicate HTTP DTO non-null checks inside handlers when the input is already validated at endpoint boundary.
- Keep endpoint/handler checks focused on values that are outside FluentValidation scope (e.g., auth claims, parsed IDs, infrastructure results).

6. Do not use exceptions for expected flow control:
- In API/Application code (`src/Harmonie.API/**`, `src/Harmonie.Application/**`), do not throw exceptions for validation, business rules, or authorization outcomes.
- Return standardized failure patterns (`ApplicationResponse` / `Result`) for expected failure paths.
- Reserve exceptions for truly unexpected technical failures only.

## Project Snapshot

Harmonie Server is a .NET 10 backend for a self-hosted communication platform.

Current implemented scope:
- Auth: register, login, and refresh
- JWT access token generation
- Refresh token persistence and rotation
- Guild creation, invitation, membership listing
- Guild channel listing
- Channel messaging (send/read) with cursor pagination
- SignalR real-time text channel delivery
- Health endpoint
- PostgreSQL persistence for users, refresh tokens, guilds, memberships, channels, and messages

## Source Layout

- `src/Harmonie.API`: startup, middleware, endpoint mapping
- `src/Harmonie.Application`: vertical slices and interfaces
- `src/Harmonie.Domain`: entities, value objects, domain rules
- `src/Harmonie.Infrastructure`: Dapper repository, JWT, password hashing
- `tools/Harmonie.Migrations`: DbUp migration runner + SQL scripts
- `tests/*`: domain, application, and API integration tests

## Active Endpoints

- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/guilds`
- `GET /api/guilds`
- `POST /api/guilds/{guildId}/members/invite`
- `GET /api/guilds/{guildId}/members`
- `GET /api/guilds/{guildId}/channels`
- `POST /api/channels/{channelId}/messages`
- `GET /api/channels/{channelId}/messages`
- `GET /hubs/text-channels` (SignalR)

## Architecture Rules

- Keep Domain independent of framework/infrastructure concerns.
- Keep Application feature-first (`Features/{Domain}/{Feature}`).
- Prefer explicit flow (endpoint -> validator -> handler -> interfaces).
- Infrastructure implements `IUserRepository`, `IPasswordHasher`, `IJwtTokenService`.

## Coding Guidelines

- Use English for code, comments, docs, and commit messages.
- Prefer clear and explicit code over abstraction-heavy patterns.
- Validate at boundaries (FluentValidation + domain invariants).
- Use async APIs end-to-end for I/O operations.
- Keep SQL parameterized (Dapper command parameters only).

## Feature Addition Checklist

1. Create a new folder under `src/Harmonie.Application/Features`.
2. Add request/validator/handler/response/endpoint files.
3. Register handler in `src/Harmonie.Application/DependencyInjection.cs`.
4. Map endpoint in `src/Harmonie.API/Program.cs`.
5. Add or update tests.
6. Update docs if behavior changed.
7. Compare against at least 2 existing features and align conventions before considering the work done.

## Testing

- Run all tests: `dotnet test`
- Integration tests use `WebApplicationFactory<Program>`.

## Reading/Search Context Rules

- Ignore build and VCS internals when reading/searching files.
- Use `.agentignore` as the source of truth for excluded paths.
- Do not load `.git`, `bin`, `obj`, or other generated artifacts into context.

## Practical Command Guidance (PowerShell)

- Prefer filtered recursive listing:
`Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|out|artifacts|TestResults)\\' -and $_.FullName -notmatch '\\\\.git\\\\' }`

## Known Gaps

- Session revocation endpoints and token reuse hardening.
- User profile self-service endpoints.
- Guild membership lifecycle completion (leave, kick, role changes, owner transfer).
- Channel lifecycle management (create/rename/reorder/delete).
- Message lifecycle management (edit/delete).

## Reference Docs

- `README.md`
- `docs/GETTING_STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/VERTICAL_SLICE_ARCHITECTURE.md`

Last updated: 2026-02-27
