# Getting Started

This guide gets the Harmonie server running locally.

## Prerequisites

- .NET 10 SDK
- Podman with `podman compose`

## 1. Start Local Services

```bash
podman compose up -d postgres livekit
```

Optional checks:

```bash
podman compose ps
podman compose logs postgres
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

The default Development config uses:
- `LiveKit:PublicUrl=ws://localhost:7880` for tokens returned to clients
- `LiveKit:InternalUrl=http://localhost:7880` for server-to-server API calls
- `ObjectStorage:LocalBasePath=uploads` for uploaded files on disk
- `ObjectStorage:LocalBaseUrl=http://localhost:5000/files` for public file URLs returned by the API

The API serves uploaded files itself from `/files/*`. In `docker-compose`, the
API container stores them in `/app/uploads`, backed by the `uploads-data`
volume.

In `docker-compose`, the API container overrides `LiveKit:InternalUrl` to `http://livekit:7880` while keeping `LiveKit:PublicUrl=ws://localhost:7880` so browser clients can still connect through the published host port.

LiveKit defaults:
- `LiveKit:RequestTimeoutSeconds` controls the timeout applied to outbound LiveKit HTTP calls. The development default is `5`.

CORS allowed origins are configured through `Cors:AllowedOrigins`.
In `Development`, the default is `["*"]`.
For non-development environments, set explicit origins in configuration or environment variables such as `Cors__AllowedOrigins__0=https://app.example.com`.

Upload storage configuration:

```json
"ObjectStorage": {
  "LocalBasePath": "/var/harmonie/uploads",
  "LocalBaseUrl": "http://localhost:5001/files"
}
```

No external object storage service is required right now. If upload volume or
deployment topology eventually justifies it, the storage abstraction can later
be backed by an S3-compatible service.

## 4. Verify the API

- Health: `GET /health`
- OpenAPI: browse Scalar API reference at `/scalar` (Development only)

## 5. Run Background Workers

The worker host is a separate executable in the same solution. It currently provides the runtime host for background notification work and can be run independently from the API.

PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/Harmonie.Workers
```

Bash:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project src/Harmonie.Workers
```

With compose, the worker service is optional and behind the `workers` profile:

```bash
podman compose --profile workers up -d harmonie-workers
```

Push notification dispatch runs in this worker host. The API only registers Web Push subscriptions and creates notification outbox jobs when messages are sent. The worker claims pending jobs and sends notifications through the configured delivery adapters.

Development Web Push configuration is provided through `VAPID_*` environment variables in compose. For local API docs, Scalar exposes the Web Push endpoints:

- `GET /api/notifications/web-push-public-key` returns the VAPID public key clients use with `PushManager.subscribe()`.
- `PUT /api/notifications/push-subscriptions` registers the resulting browser subscription.

Current backend notification behavior:
- conversation messages notify participants except the author;
- guild channel messages notify channel candidate members except the author;
- payloads are minimal `message.created` business data, without message content or UI presentation fields;
- retries are tracked per device, so a partial Web Push failure does not resend to devices that already succeeded.

Notification cleanup is configured and enabled by default in the worker host. Adjust the retention periods for operational debugging, or set `Enabled` to `false` to disable it:

```json
"NotificationCleanup": {
  "Enabled": true,
  "PollIntervalSeconds": 1800,
  "BatchSize": 500,
  "ProcessedOutboxRetentionDays": 7,
  "FailedOutboxRetentionDays": 30,
  "ExpiredDeviceRetentionDays": 7
}
```

The worker deletes terminal outbox jobs and expired notification devices in small, multi-instance-safe batches. Failed jobs use a longer default retention period for troubleshooting.

## 6. Run Tests

```bash
dotnet test
```

The upload tests use the local filesystem provider. No external object storage service is required.

## Notes

- OpenAPI and Scalar API reference are enabled only in Development.
- Refresh tokens are persisted and rotated in storage.
- Message posting is rate-limited (`40` messages/minute per authenticated user).
- If the API cannot connect to PostgreSQL, confirm `podman compose` is running and port `5432` is available.
- If the voice snapshot tests fail, confirm LiveKit is running on port `7880`.
- If you later need multi-node or externalized file storage, an S3-compatible backend can be introduced behind `IObjectStorageService`.
