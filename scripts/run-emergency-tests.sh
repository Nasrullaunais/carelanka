#!/usr/bin/env bash
# Runs every Emergency test: backend and database (Docker must be running), web, then Flutter.
set -uo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"

p=CareLanka.Api.Tests
filter="FullyQualifiedName~$p.Emergency|FullyQualifiedName~$p.Dispatch|FullyQualifiedName~$p.Ambulance"
filter="$filter|FullyQualifiedName~$p.Osrm|FullyQualifiedName~$p.Nominatim|FullyQualifiedName~$p.DemoFleet"

status=0
echo "=== 1/3 Backend and database (xUnit) ==="
dotnet test "$repo/tests/CareLanka.Api.Tests" --filter "$filter" || status=1

echo "=== 2/3 Web (Vitest) ==="
(cd "$repo/web-ui" && npx vitest run src/features/emergency) || status=1

echo "=== 3/3 Flutter ==="
(cd "$repo/mobile-ui" && flutter test test/features/emergency) || status=1

exit $status
