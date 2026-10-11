# OWASP ZAP passive + short active scan of the Patient routes, against the LOCAL Docker stack only.
#   .\scripts\run-patient-zap-scan.ps1                  patient (/me) routes, 3-minute active scan
#   .\scripts\run-patient-zap-scan.ps1 -Who staff       staff routes, starting at /patients
#   .\scripts\run-patient-zap-scan.ps1 -Minutes 5       longer active scan
# The HTML report is saved in tests/performance/patient/results/zap.
param(
    [ValidateSet('patient', 'staff')]
    [string]$Who = 'patient',

    [int]$Minutes = 3
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$perf = "$repo/tests/performance/patient"
$zapBat = 'C:\Program Files\ZAP\Zed Attack Proxy\zap.bat'
$zapUrl = 'http://127.0.0.1:8080'
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
    Write-Host "`n=== Creating the throwaway logins in the local database ===" -ForegroundColor Cyan
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

function Test-Zap {
    try { Invoke-RestMethod "$zapUrl/JSON/core/view/version/" -TimeoutSec 3 | Out-Null; $true } catch { $false }
}

if (-not (Test-Zap)) {
    Write-Host "`n=== Opening ZAP (first start takes about a minute) ===" -ForegroundColor Cyan
    # -newsession skips the "save this session?" pop-up, which otherwise blocks ZAP from starting.
    $session = "$perf/results/zap/session-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')"
    New-Item -ItemType Directory -Force (Split-Path $session) | Out-Null
    Start-Process -FilePath $zapBat -ArgumentList '-config', 'api.disablekey=true', '-newsession', "`"$session`"" -WorkingDirectory (Split-Path $zapBat)
    while (-not (Test-Zap)) {
        Start-Sleep -Seconds 3
    }
}
Write-Host 'ZAP is ready.'

$zap = "$perf/zap/zap.ps1"

Write-Host "`n=== 1/2 Preparing the address list ===" -ForegroundColor Cyan
& $zap prepare
if ($LASTEXITCODE -ne 0) { throw 'prepare failed. Nothing was scanned.' }

Write-Host "`n=== 2/2 Passive scan, then active scan ($Who routes) ===" -ForegroundColor Cyan
& $zap scan $Who -Minutes $Minutes
if ($LASTEXITCODE -ne 0) { throw 'The scan stopped with an error (see above).' }
