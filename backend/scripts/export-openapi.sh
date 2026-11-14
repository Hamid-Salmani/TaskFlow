#!/usr/bin/env bash
# Exports the OpenAPI document of the API to backend/openapi.json (used by the frontend: `npm run api:generate`).
# Works without a running server: the Swashbuckle CLI loads the application and asks the document provider directly.
set -euo pipefail

cd "$(dirname "$0")/.."

dotnet tool restore >/dev/null
dotnet build src/TaskFlow.Api -c Release --nologo -v q

# The signing key is only needed so the host can be constructed; nothing is signed during the export.
ASPNETCORE_ENVIRONMENT=Development \
Jwt__Key="export-only-signing-key-not-used-at-runtime-0123456789" \
ConnectionStrings__Default="Data Source=:memory:" \
Database__SkipInitialization=true \
  dotnet tool run swagger tofile --output openapi.json src/TaskFlow.Api/bin/Release/net8.0/TaskFlow.Api.dll v1

echo "Wrote $(pwd)/openapi.json"
