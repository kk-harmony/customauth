#!/usr/bin/env bash
# Start Postgres in Docker, apply SQL migrations, run integration tests.
set -euo pipefail
cd "$(dirname "$0")/.."

COMPOSE_FILE="docker-compose.test.yml"
export CUSTOMOAUTH_TEST_CONNECTION="${CUSTOMOAUTH_TEST_CONNECTION:-Host=localhost;Port=5432;Database=customoauth;Username=postgres;Password=postgres}"

echo "Starting PostgreSQL for tests..."
docker compose -f "$COMPOSE_FILE" up -d --wait

echo "Applying SQL schema..."
./scripts/apply-sql-migrations.sh

echo "Running tests..."
dotnet test CustomOAuthServer.sln -c Release "$@"
