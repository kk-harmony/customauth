#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet tool restore
dotnet ef migrations add "$1" \
  --project src/CustomOAuthServer.Infrastructure \
  --startup-project src/CustomOAuthServer.Api

echo "Migration created. Commit src/CustomOAuthServer.Infrastructure/Migrations/ before deploying to Production."
