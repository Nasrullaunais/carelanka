#!/usr/bin/env bash
# Active ZAP API scan of the Emergency read routes for one role: ./run-zap.sh manager|patient|crew
# Needs BASE_URL and the QM_* logins exported (same as run-k6.sh). Only GET routes are scanned.
set -euo pipefail

role="${1:?Usage: ./run-zap.sh manager|patient|crew}"
cd "$(dirname "$0")"
mkdir -p reports
base="${BASE_URL:?BASE_URL is not set}"
base="${base%/}"

case "$role" in
  manager) token=$(curl -fsS "$base/api/auth/login" -H 'Content-Type: application/json' \
             -d "{\"email\":\"$QM_DM_USER\",\"password\":\"$QM_DM_PASSWORD\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["access_token"])') ;;
  crew)    token=$(curl -fsS "$base/api/auth/login" -H 'Content-Type: application/json' \
             -d "{\"email\":\"$QM_CREW_USER\",\"password\":\"$QM_CREW_PASSWORD\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["access_token"])') ;;
  patient) token=$(curl -fsS "$base/api/auth/patient/login" -H 'Content-Type: application/json' \
             -d "{\"username\":\"$QM_PATIENT_USER\",\"password\":\"$QM_PATIENT_PASSWORD\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["access_token"])') ;;
  *) echo "Unknown role $role" >&2; exit 1 ;;
esac

curl -fsS "$base/swagger/v1/swagger.json" -o reports/live-swagger.json
python3 real-examples.py "$base" "$token" "$role" > "reports/examples-$role.json"
python3 emergency-routes.py reports/live-swagger.json "$role" "reports/emergency-$role.json" "$base" "reports/examples-$role.json"

stamp=$(date -u +%Y-%m-%dT%H-%M)
echo "Started ZAP $role scan: $(date '+%Y-%m-%d %H:%M:%S %z')"
# The login token lasts 15 minutes, so the active scan is stopped at 12.
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD/reports:/zap/wrk:rw" \
  -e ZAP_AUTH_HEADER=Authorization -e ZAP_AUTH_HEADER_VALUE="Bearer $token" \
  zaproxy/zap-stable zap-api-scan.py \
  -t "/zap/wrk/emergency-$role.json" -f openapi \
  -r "zap-$role-$stamp.html" -J "zap-$role-$stamp.json" -w "zap-$role-$stamp.md" \
  -I -z "-config scanner.maxScanDurationInMins=12 -config scanner.threadPerHost=2 -config scanner.delayInMs=100"
