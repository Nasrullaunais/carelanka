# .\scripts\run-patient-agent-tests.ps1         rule tests, then review tests
# .\scripts\run-patient-agent-tests.ps1 -Live   also the live Gemini test (PT-AI-15, up to 8 real calls)
param([switch]$Live)

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
$project = "$repo/tests/CareLanka.Api.Tests/CareLanka.Api.Tests.csproj"
$total = if ($Live) { 3 } else { 2 }
Push-Location $repo

Write-Host "`n=== 1/$total Agent rule tests: PatientCareAgentRuleTests ===" -ForegroundColor Cyan
dotnet test $project --filter "FullyQualifiedName~.PatientCareAgentRuleTests" --logger "console;verbosity=normal"

Write-Host "`n=== 2/$total Agent review tests: PatientCareAgentReviewTests (Docker Desktop must be running) ===" -ForegroundColor Cyan
dotnet test $project --filter "FullyQualifiedName~.PatientCareAgentReviewTests" --logger "console;verbosity=normal"

if ($Live) {
    Write-Host "`n=== 3/3 Live Gemini test: PT-AI-15 (key from .env) ===" -ForegroundColor Cyan
    $env:QM_LIVE_GEMINI = '1'
    try {
        dotnet test $project --filter "id=PT-AI-15" --logger "console;verbosity=normal"
    }
    finally {
        Remove-Item Env:QM_LIVE_GEMINI -ErrorAction SilentlyContinue
    }

    # The 7 questions and Gemini's replies go to a file, not the test output.
    $results = "$repo/AssignmentDocs/PT-AI-15_live-results.md"
    if (Test-Path $results) {
        Write-Host "`n=== Live results: $results ===" -ForegroundColor Cyan
        Get-Content $results
    }
}

Pop-Location
