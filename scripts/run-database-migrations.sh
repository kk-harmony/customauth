#!/usr/bin/env bash
# Applies SQL scripts then OpenIddict EF migrations (when present).
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
  echo "No EF migrations folder; skip dotnet ef database update (use EnsureCreated in Development only)."
fi
