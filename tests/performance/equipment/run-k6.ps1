param(
    [Parameter(Mandatory)]
    [ValidateSet('smoke', 'baseline', 'ramp', 'stress')]
    [string]$Scenario
)

if (-not $env:BASE_URL) {
    throw 'BASE_URL is not set in this session. Set it first, then run this again.'
}
if (-not $env:EQ_STAFF_USER -or -not $env:EQ_STAFF_PASSWORD) {
    throw 'EQ_STAFF_USER / EQ_STAFF_PASSWORD are not set in this session. Set them first (the equipment manager seed account works), then run this again.'
}

$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$env:EQ_RUN_STAMP = $stamp

Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path 'results' | Out-Null
    Write-Host "Scenario: $Scenario"
    Write-Host "Started:  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    k6 run "$Scenario.js"
    Write-Host "Finished: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
}
finally {
    Pop-Location
    Remove-Item Env:EQ_RUN_STAMP -ErrorAction SilentlyContinue
}
