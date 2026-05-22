#!/usr/bin/env bash
# Applies Migrations/Sql/*.sql (including OpenIddict), then EF migrations if committed.
set -euo pipefail
cd "$(dirname "$0")/.."

./scripts/apply-sql-migrations.sh

if [[ -d src/CustomOAuthServer.Infrastructure/Migrations ]]; then
  dotnet tool restore
  dotnet ef database update \
    --project src/CustomOAuthServer.Infrastructure \
    --startup-project src/CustomOAuthServer.Api
  echo "EF migrations complete."
else
  echo "No EF migrations folder; OpenIddict schema should come from 003_openiddict.sql (or EnsureCreated in Development)."
fi
