#!/usr/bin/env bash
# Runs one Emergency k6 scenario in Docker: ./run-k6.sh smoke|ramp|spike
# Needs BASE_URL and the QM_* logins exported in the shell; nothing secret is stored here.
set -euo pipefail

scenario="${1:?Usage: ./run-k6.sh smoke|ramp|spike}"
cd "$(dirname "$0")"
mkdir -p results

for name in BASE_URL QM_DM_USER QM_DM_PASSWORD QM_CREW_USER QM_CREW_PASSWORD QM_PATIENT_USER QM_PATIENT_PASSWORD; do
  if [ -z "${!name:-}" ]; then
    echo "$name is not set." >&2
    exit 1
  fi
done

curl -fsS "${BASE_URL%/}/api/health" > /dev/null || { echo "Health check failed. Not starting." >&2; exit 1; }
echo "Started $scenario: $(date '+%Y-%m-%d %H:%M:%S %z')"

docker run --rm -i --user "$(id -u):$(id -g)" \
  -v "$PWD:/scripts" -w /scripts \
  -e BASE_URL -e QM_DM_USER -e QM_DM_PASSWORD -e QM_CREW_USER -e QM_CREW_PASSWORD \
  -e QM_PATIENT_USER -e QM_PATIENT_PASSWORD \
  grafana/k6:latest run "$scenario.js"
