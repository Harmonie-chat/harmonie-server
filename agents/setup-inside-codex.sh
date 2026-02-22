#!/usr/bin/env bash
set -euo pipefail

DB_USER="${DB_USER:-harmonie_user}"
DB_PASSWORD="${DB_PASSWORD:-harmonie_password}"
DB_NAME="${DB_NAME:-harmonie_test}"

CONNECTION_STRING="${ConnectionStrings__DefaultConnection:-Host=localhost;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}}"

# Start PostgreSQL from the image.
pg_ctlcluster 16 main start

# Ensure role exists (or reset its password to expected value).
if ! runuser -u postgres -- psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = '${DB_USER}'" | grep -q 1; then
  runuser -u postgres -- psql -c "CREATE ROLE ${DB_USER} LOGIN PASSWORD '${DB_PASSWORD}';"
else
  runuser -u postgres -- psql -c "ALTER ROLE ${DB_USER} WITH LOGIN PASSWORD '${DB_PASSWORD}';"
fi

# Ensure database exists.
if ! runuser -u postgres -- psql -tAc "SELECT 1 FROM pg_database WHERE datname = '${DB_NAME}'" | grep -q 1; then
  runuser -u postgres -- psql -c "CREATE DATABASE ${DB_NAME} OWNER ${DB_USER};"
fi

cd /workspace

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ConnectionStrings__DefaultConnection="${CONNECTION_STRING}"

# Mirror CI workflow steps.
dotnet run --project tools/Harmonie.Migrations/Harmonie.Migrations.csproj --configuration Release
dotnet build --configuration Release
dotnet test --no-build --verbosity normal --configuration Release
