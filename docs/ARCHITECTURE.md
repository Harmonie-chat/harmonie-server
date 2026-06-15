# Architecture Overview

Harmonie follows a Clean Architecture direction with Vertical Slice organization in the Application layer.

## Layers

## API (`src/Harmonie.API`)

Responsibilities:
- Application startup and middleware pipeline
- JWT authentication and authorization wiring
- OpenAPI/Scalar setup (Development only)
- Endpoint mapping entry point

## Application (`src/Harmonie.Application`)

Responsibilities:
- Feature slices (request, validator, handler, endpoint)
- Orchestration of domain logic through interfaces
- Validation registration and handler DI registration

Features are organized under `Features/{Domain}/{Feature}`.

Shared:
- `Common/IEndpoint.cs`
- `Common/EndpointExtensions.cs`
- `Interfaces/*` ports for repository/hash/token services

## Domain (`src/Harmonie.Domain`)

Responsibilities:
- Business model and invariants
- Entities, value objects
- Domain exceptions and events
- Result pattern (`Common/Result.cs`)

This layer has no dependency on application/infrastructure/web concerns.

## Infrastructure (`src/Harmonie.Infrastructure`)

Responsibilities:
- Adapter implementations for application interfaces
- Dapper-based repositories
- JWT generation/validation and password hashing
- Options/configuration objects
- Modular DI registration so API and worker hosts can opt into only the infrastructure they need

## Workers (`src/Harmonie.Workers`)

Responsibilities:
- Independent background worker host
- Long-running/background orchestration that should not share the HTTP API process
- Reuses Application services and Infrastructure adapters without depending on `Harmonie.API`

Current worker jobs:
- Push notification dispatch from `message_notification_outbox`

## Push Notifications

Push notifications are asynchronous and transport-agnostic at the Application boundary:

1. Message creation writes the message and a `message_notification_outbox` row in the same transaction.
2. `Harmonie.Workers` claims pending outbox jobs with a lock/retry policy.
3. `MessageNotificationPolicy` selects users to notify without knowing the delivery platform.
4. `NotificationDispatchService` loads active notification devices for selected users and records per-device delivery state in `message_notification_deliveries`.
5. Platform adapters deliver the minimal business payload only to pending/retryable device deliveries. Succeeded device deliveries are not retried, which prevents duplicate pushes after partial failures. Web Push is the first adapter; Android FCM and iOS APNs can be added behind the same device/policy model later.

Initial message notification policy:
- Direct conversation messages: notify participants except the author.
- Group conversation messages: notify participants except the author.
- Guild channel messages: notify channel candidate members except the author.
- Guild channel mention-only notification preferences can be added later without changing the delivery adapters.

The Web Push payload intentionally excludes message content and presentation fields. Clients/service workers own notification text, routing, i18n, icons, badges, and tags.

## Data and Migration Strategy

- DB: PostgreSQL
- Access: Dapper + Npgsql
- Migrations: DbUp runner in `tools/Harmonie.Migrations`
- Scripts located in `tools/Harmonie.Migrations/Scripts/`

## Request Flow (Example: Send Message)

1. Endpoint receives request (`SendMessageEndpoint`)
2. FluentValidation validates request DTO
3. Handler loads channel and membership permissions
4. Domain value objects/entities enforce message rules
5. Repository persists message in a transaction
6. SignalR notifier emits best-effort `MessageCreated` event

## Cross-Cutting Concerns

- Standardized internal application result flow: `ApplicationResponse<T>` in handlers
- Success HTTP responses return feature DTOs directly
- Standardized error contract: `ApplicationError` (`code`, `message`, `details`)
- Global exception handling (unexpected errors fallback): `src/Harmonie.API/Middleware/GlobalExceptionHandler.cs`
- Structured logging: Serilog
- Authentication: JWT bearer middleware
- Authorization: membership checks in handlers and SignalR hub methods
- Throttling: fixed-window limiter for message posting (`message-post` policy)
