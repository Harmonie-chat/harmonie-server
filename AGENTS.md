# AGENTS Rules - Harmonie Server

Scope: canonical instructions for AI coding agents working in this repository.
For project scope, endpoints, and structure see `README.md` and `docs/`.

## Issue Workflow

When picking up a GitHub issue:

1. **Start**: Create a new branch from `main` following the naming convention below before making any changes.
2. **Finish**: Once the work is done and tests pass, commit all changes, push the branch, and open a Pull Request targeting `main`.

## Branch Naming Convention

- Feature branches must follow: `feat/{issueNumber}-{FeatureName}`.
- `issueNumber` is the GitHub issue number the branch addresses.
- `FeatureName` is PascalCase, derived from the issue title (no spaces).
- Example: `feat/20-LeaveGuild`, `feat/42-UserProfile`.

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

7. Migration table changes require backfill:
- For any SQL migration that adds/changes table structure or relationships used by existing data flows, include a backfill strategy in migration scripts.
- Backfill must be delivered as part of the migration set (same rollout), not postponed.
- If exact backfill is impossible, add an explicit fail-safe migration/logic that prevents stale data from creating a security or business inconsistency.

8. Migration script naming convention:
- New migration script files must follow: `date_numberThatDay_scriptName.sql`.
- `date` format: `yyyyMMdd`.
- `numberThatDay` resets each day (1, 2, 3, ...), so numbering is local to the date and does not keep growing globally.
- Example: `20260227_1_AddRefreshTokenMetadata.sql`, `20260227_2_BackfillRefreshTokenLinks.sql`.

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

- Prefer `podman` / `podman compose` over Docker / Docker Compose for local container workflows in this repository.
- Prefer filtered recursive listing:
`Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|out|artifacts|TestResults)\\' -and $_.FullName -notmatch '\\\\.git\\\\' }`

## Reference Docs

- `README.md` — current scope, endpoints, stack, scalability TODOs
- `docs/GETTING_STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/VERTICAL_SLICE_ARCHITECTURE.md`

Last updated: 2026-03-08
