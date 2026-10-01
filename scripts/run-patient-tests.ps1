$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo

$backendClasses = @(
    'AdmissionEndpointTests', 'AdmissionStatusMachineTests', 'AppointmentEndpointTests',
    'BedAssignmentEndpointTests', 'BillingRateEndpointTests', 'CapacityEndpointTests',
    'CareAdvisorTests', 'CareAgentJournalTests', 'CareAgentTests',
    'CareRecommendationEndpointTests', 'CareRecommendationValidatorTests', 'CareRedFlagScreenTests',
    'DischargeBillingEndpointTests', 'MedicalProfileEndpointTests',
    'MeEndpointTests', 'PatientAppAccountEndpointTests', 'PatientEndpointTests',
    'PatientNotificationTests', 'PatientOpenApiContractTests', 'WardEndpointTests',
    'WorklistEndpointTests'
)
$filter = ($backendClasses | ForEach-Object { "FullyQualifiedName~.$_" }) -join '|'

$webFiles = @(
    'src/pages/PatientAppAccountsPage.test.tsx',
    'src/types/identifiers.test.ts'
)

Write-Host "`n=== 1/3 Backend xUnit (Docker Desktop must be running) ===" -ForegroundColor Cyan
dotnet test "$repo/tests/CareLanka.Api.Tests/CareLanka.Api.Tests.csproj" --filter $filter

Write-Host "`n=== 2/3 Web Vitest ===" -ForegroundColor Cyan
Push-Location "$repo/web-ui"
npx vitest run @webFiles
Pop-Location

Write-Host "`n=== 3/3 Flutter ===" -ForegroundColor Cyan
Push-Location "$repo/mobile-ui"
flutter test test/features/patient
Pop-Location

Pop-Location
