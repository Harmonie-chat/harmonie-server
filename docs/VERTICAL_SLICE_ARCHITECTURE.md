# Vertical Slice Architecture

Harmonie organizes the Application layer by feature slices rather than by technical folders (controllers/services/repositories).

## Slice Structure

Each feature keeps its HTTP contract, validation, and business orchestration together.

Example (`Auth/Register`):
- `RegisterRequest.cs`
- `RegisterValidator.cs`
- `RegisterHandler.cs`
- `RegisterResponse.cs`
- `RegisterEndpoint.cs`

Current auth slices:
- `Features/Auth/Register`
- `Features/Auth/Login`
- `Features/Auth/RefreshToken` (request/response contracts only)

## Why This Layout

- Faster navigation: all feature pieces are in one folder
- Lower coupling between unrelated features
- Explicit request flow without mediator indirection
- Easier incremental delivery (add one feature folder at a time)

## Runtime Flow in This Project

1. Endpoint maps route and handles HTTP concerns.
2. Validator checks request shape and basic rules.
3. Handler performs use-case logic via interfaces.
4. Infrastructure implementations execute persistence and token logic.

## Endpoint Mapping Pattern

Endpoints are mapped from `Program.cs`:

- `RegisterEndpoint.Map(app);`
- `LoginEndpoint.Map(app);`

As new slices are added, map their endpoints in the same place.

## How to Add a New Slice

1. Create a folder in `src/Harmonie.Application/Features/{Domain}/{Feature}`.
2. Add request, validator, handler, response, and endpoint classes.
3. Register the handler in `src/Harmonie.Application/DependencyInjection.cs`.
4. Map the endpoint from `src/Harmonie.API/Program.cs`.
5. Add tests in the appropriate test project.

## Current Boundaries

- Domain remains framework-agnostic.
- Application depends on Domain only.
- Infrastructure depends on Application + Domain.
- API depends on Application + Infrastructure.
