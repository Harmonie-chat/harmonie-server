#!/usr/bin/env bash
set -euo pipefail

DEFAULT_CONNECTION_STRING="Host=localhost;Port=5432;Database=harmonie;Username=harmonie_user;Password=harmonie_password"
CONNECTION_STRING="${ConnectionStrings__DefaultConnection:-$DEFAULT_CONNECTION_STRING}"

get_connection_value() {
  local key="$1"
  local segment
  IFS=';' read -ra segments <<< "$CONNECTION_STRING"
  for segment in "${segments[@]}"; do
    segment="${segment#"${segment%%[![:space:]]*}"}"
    segment="${segment%"${segment##*[![:space:]]}"}"
    if [[ "$segment" == "$key="* ]]; then
      echo "${segment#*=}"
      return 0
    fi
  done
  return 1
}

DB_NAME="${DB_NAME:-$(get_connection_value "Database" || echo "harmonie")}"
DB_USER="${DB_USER:-$(get_connection_value "Username" || echo "harmonie_user")}"
DB_PASSWORD="${DB_PASSWORD:-$(get_connection_value "Password" || echo "harmonie_password")}"

# Start PostgreSQL from the image.
if ! pg_ctlcluster 16 main status >/dev/null 2>&1; then
  pg_ctlcluster 16 main start
fi

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
