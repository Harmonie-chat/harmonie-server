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

## 5. Run Tests

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
