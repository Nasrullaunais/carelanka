$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo

Write-Host "`n=== Database tests: PatientDatabaseTests (Docker Desktop must be running) ===" -ForegroundColor Cyan
dotnet test "$repo/tests/CareLanka.Api.Tests/CareLanka.Api.Tests.csproj" `
    --filter "FullyQualifiedName~.PatientDatabaseTests" `
    --logger "console;verbosity=normal"

Pop-Location
