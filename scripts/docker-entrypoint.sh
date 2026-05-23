#!/usr/bin/env bash
set -euo pipefail

if [[ "${ASPNETCORE_ENVIRONMENT:-}" == "Production" \
  && "${OAUTH_AUTO_GENERATE_SIGNING_CERT:-true}" != "false" ]]; then
  /app/scripts/ensure-signing-cert.sh
fi

exec dotnet /app/CustomOAuthServer.Api.dll "$@"
