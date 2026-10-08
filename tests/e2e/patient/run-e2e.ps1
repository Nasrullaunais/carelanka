# Runs E2E-01 on the local Docker stack.
#   .\run-e2e.ps1           normal run
#   .\run-e2e.ps1 -Headed   visible, slowed-down browser for a live demo
#
# The API reads the Gemini key from the gitignored .env file.
# Out of free beds? From the repository root: docker compose down -v, then docker compose up -d --build
# (this deletes the LOCAL database only and loads the demo data again).

param([switch]$Headed)

# 1. Start the database and API, then wait until the API answers.
docker compose -f "$PSScriptRoot\..\..\..\compose.yaml" up -d
while (-not (Invoke-RestMethod 'http://localhost:5231/api/health' -ErrorAction SilentlyContinue 2>$null)) {
    Start-Sleep -Seconds 3
}

# 2. Run the test.
Push-Location $PSScriptRoot
if (-not (Test-Path 'node_modules')) { npm ci; npx playwright install chromium }
if ($Headed) {
    $env:E2E_SLOW_MO = '400'
    npx playwright test --headed
    Remove-Item Env:E2E_SLOW_MO
} else {
    npx playwright test
}

# 3. Show the report in the browser (Ctrl+C to stop).
npx playwright show-report results\html-report
Pop-Location
