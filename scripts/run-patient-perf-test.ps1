# Runs one k6 scenario against the LOCAL Docker stack only, never the shared hosted server.
#   .\scripts\run-patient-perf-test.ps1                    smoke (3 users, 30 seconds)
#   .\scripts\run-patient-perf-test.ps1 -Scenario ramp     baseline, ramp, stress, patient-reads, login-flood
#   .\scripts\run-patient-perf-test.ps1 -Scenario all      every scenario except stress, one after another
# The summary is saved in tests/performance/patient/results.
param(
    [ValidateSet('smoke', 'baseline', 'ramp', 'stress', 'patient-reads', 'login-flood', 'all')]
    [string]$Scenario = 'smoke'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$perf = "$repo/tests/performance/patient"
$env:BASE_URL = 'http://localhost:5231'

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Desktop is not running. Start it, wait until the engine is running, then run this again.'
}

Write-Host "`n=== Starting the local server and database ===" -ForegroundColor Cyan
docker compose -f "$repo/compose.yaml" up -d
while (-not (Invoke-RestMethod "$env:BASE_URL/api/health" -ErrorAction SilentlyContinue 2>$null)) {
    Start-Sleep -Seconds 3
}
Write-Host 'Local server is up.'

if (-not [Environment]::GetEnvironmentVariable('QM_STAFF_USER', 'User')) {
    Write-Host "`n=== Creating the throwaway k6 logins in the local database ===" -ForegroundColor Cyan
    # The seeded local administrator from TEST_ACCOUNTS.md. It only exists in the local demo data.
    $env:QM_ADMIN_USER = 'admin.wickrama@carelanka.lk'
    $env:QM_ADMIN_PASSWORD = 'CareLanka#2026'
    try {
        & "$perf/setup-accounts.ps1"
    }
    finally {
        Remove-Item Env:QM_ADMIN_USER, Env:QM_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    }
}

# Stress is left out of "all": 250 users on the same laptop as the server measures the laptop, not the API.
$scenarios = if ($Scenario -eq 'all') {
    'smoke', 'baseline', 'ramp', 'patient-reads', 'login-flood'
} else {
    , $Scenario
}

$i = 0
foreach ($name in $scenarios) {
    $i++
    Write-Host "`n=== k6 $i/$($scenarios.Count): $name ===" -ForegroundColor Cyan
    & "$perf/run-k6.ps1" -Scenario $name
}
