param(
    [Parameter(Mandatory)]
    [ValidateSet('prepare', 'token', 'scan')]
    [string]$Command,

    [ValidateSet('staff', 'patient', 'clear')]
    [string]$Who,

    [int]$Minutes = 3
)

if (-not $env:BASE_URL) {
    throw 'BASE_URL is not set in this session. Set it first, then run this again.'
}
if ($Command -in 'token', 'scan' -and -not $Who) {
    throw 'Say who: staff or patient (token also takes clear).'
}

# A session started before setup-accounts.ps1 ran does not see the variables it saved.
foreach ($name in 'QM_STAFF_USER', 'QM_STAFF_PASSWORD', 'QM_PATIENT_USER', 'QM_PATIENT_PASSWORD') {
    $saved = [Environment]::GetEnvironmentVariable($name, 'User')
    if ($saved) {
        Set-Item -Path "Env:$name" -Value $saved
    }
}

Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
node (Join-Path $PSScriptRoot 'zap.mjs') $Command $Who $Minutes
exit $LASTEXITCODE
