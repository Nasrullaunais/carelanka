param(
    [Parameter(Mandatory)]
    [ValidateSet('smoke', 'baseline', 'ramp', 'stress', 'patient-reads', 'login-flood')]
    [string]$Scenario
)

if (-not $env:BASE_URL) {
    throw 'BASE_URL is not set in this session. Set it first, then run this again.'
}

# A session started before setup-accounts.ps1 ran does not see the variables it saved.
foreach ($name in 'QM_STAFF_USER', 'QM_STAFF_PASSWORD', 'QM_PATIENT_USER', 'QM_PATIENT_PASSWORD') {
    $saved = [Environment]::GetEnvironmentVariable($name, 'User')
    if ($saved) {
        Set-Item -Path "Env:$name" -Value $saved
    }
}

$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$env:QM_RUN_STAMP = $stamp

Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path 'results' | Out-Null
    Write-Host "Scenario: $Scenario"
    Write-Host "Started:  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    k6 run --console-output "results/created-ids-$Scenario-$stamp.log" "$Scenario.js"
    Write-Host "Finished: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
}
finally {
    Pop-Location
    Remove-Item Env:QM_RUN_STAMP -ErrorAction SilentlyContinue
}
