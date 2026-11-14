#!/usr/bin/env bash
# Runs all backend tests with coverage of the Application layer and fails below the threshold (default 80% lines).
# Unit tests and the HTTP integration tests both exercise the handlers, so their reports are merged.
set -euo pipefail

cd "$(dirname "$0")/.."
THRESHOLD="${COVERAGE_THRESHOLD:-80}"

rm -rf coverage TestResults
dotnet tool restore >/dev/null
dotnet test TaskFlow.sln --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults

dotnet tool run reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:coverage \
  -reporttypes:"TextSummary;Html;Cobertura" \
  -assemblyfilters:"+TaskFlow.Application" >/dev/null

cat coverage/Summary.txt
LINE_RATE=$(grep -m1 "Line coverage:" coverage/Summary.txt | sed -E 's/[^0-9.]*([0-9.]+)%.*/\1/')

if awk -v rate="$LINE_RATE" -v min="$THRESHOLD" 'BEGIN { exit !(rate + 0 < min + 0) }'; then
  echo "Line coverage of TaskFlow.Application is ${LINE_RATE}%, below the ${THRESHOLD}% threshold." >&2
  exit 1
fi
echo "Line coverage of TaskFlow.Application: ${LINE_RATE}% (threshold ${THRESHOLD}%)."
