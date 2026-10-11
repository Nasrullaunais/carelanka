# .\scripts\run-patient-e2e-test.ps1           Playwright E2E-01 on the local Docker stack
# .\scripts\run-patient-e2e-test.ps1 -Headed   visible, slowed-down browser for a live demo
param([switch]$Headed)

& "$PSScriptRoot/../tests/e2e/patient/run-e2e.ps1" -Headed:$Headed
