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

## Application (`src/Harmonie.Application`)

Responsibilities:
- Feature slices (request, validator, handler, endpoint)
- Orchestration of domain logic through interfaces
- Validation registration and handler DI registration

Current features:
- `Features/Auth/Register/*`
- `Features/Auth/Login/*`
- `Features/Auth/RefreshToken/*` (contracts only)

Shared:
- `Common/IEndpoint.cs`
- `Common/EndpointExtensions.cs`
- `Interfaces/*` ports for repository/hash/token services

## Domain (`src/Harmonie.Domain`)

Responsibilities:
- Business model and invariants
- Entities (`User`)
- Value objects (`Email`, `Username`, `UserId`)
- Domain exceptions and events
- Result pattern (`Common/Result.cs`)

This layer has no dependency on application/infrastructure/web concerns.

## Infrastructure (`src/Harmonie.Infrastructure`)

Responsibilities:
- Adapter implementations for application interfaces
- Dapper-based `UserRepository`
- JWT generation/validation and password hashing
- Options/configuration objects

## Data and Migration Strategy

- DB: PostgreSQL
- Access: Dapper + Npgsql
- Migrations: DbUp runner in `tools/Harmonie.Migrations`
- Initial schema script: `tools/Harmonie.Migrations/Scripts/20260215_01_CreateUsersTable.sql`

## Request Flow (Auth)

1. Endpoint receives request (`RegisterEndpoint` or `LoginEndpoint`)
2. FluentValidation validates request DTO
3. Handler executes business flow
4. Domain value objects/entities enforce domain rules
5. Repository persists/reads user data
6. JWT service generates tokens

## Cross-Cutting Concerns

- Global exception handling: `src/Harmonie.API/Middleware/GlobalExceptionHandler.cs`
- Structured logging: Serilog
- Authentication: JWT bearer middleware

## Known Gaps

- Refresh token persistence/rotation/revocation is not implemented yet.
- Only auth and health endpoints are currently available.
