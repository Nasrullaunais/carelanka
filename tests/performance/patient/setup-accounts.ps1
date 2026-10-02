#requires -Version 5.1
<#
Creates the two throwaway accounts that k6 and ZAP log in as:
  staff    qm-pt-staff@carelanka.lk  (duty manager)
  patient  qm-pt-patient             (linked to the record "QM Test qm-pt-patient")

Reads BASE_URL, QM_ADMIN_USER and QM_ADMIN_PASSWORD from the session. Saves the generated
logins as Windows user-level variables only. Safe to run again: anything that already works
is skipped. It never resets a password it did not create.
#>

$ErrorActionPreference = 'Stop'

$StaffEmail = 'qm-pt-staff@carelanka.lk'
$PatientUsername = 'qm-pt-patient'
$PatientRecordName = 'QM Test qm-pt-patient'

foreach ($required in 'BASE_URL', 'QM_ADMIN_USER', 'QM_ADMIN_PASSWORD') {
    if (-not (Get-Item -Path "Env:$required" -ErrorAction SilentlyContinue)) {
        throw "$required is not set in this session."
    }
}
$base = $env:BASE_URL.TrimEnd('/')

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token)

    $params = @{
        Method          = $Method
        Uri             = "$base$Path"
        Headers         = @{}
        ContentType     = 'application/json'
        UseBasicParsing = $true
    }
    if ($Token) { $params.Headers['Authorization'] = "Bearer $Token" }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 5 -Compress) }

    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        if (-not $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        $content = $_.ErrorDetails.Message
    }

    $json = $null
    if ($content) {
        try { $json = $content | ConvertFrom-Json } catch { $json = $null }
    }
    [pscustomobject]@{ Status = $status; Json = $json }
}

function Stop-WithApiError {
    param([string]$Step, $Result)

    $code = if ($Result.Json -and $Result.Json.code) { " code=$($Result.Json.code)" } else { '' }
    $title = if ($Result.Json -and $Result.Json.title) { " ($($Result.Json.title))" } else { '' }
    throw "$Step failed: HTTP $($Result.Status)$code$title"
}

function New-Password {
    $chars = 'ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789'
    $limit = 256 - (256 % $chars.Length)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 64
    do {
        $text = ''
        while ($text.Length -lt 28) {
            $rng.GetBytes($bytes)
            foreach ($b in $bytes) {
                if ($b -lt $limit -and $text.Length -lt 28) { $text += $chars[$b % $chars.Length] }
            }
        }
    } until ($text -cmatch '[a-z]' -and $text -cmatch '[A-Z]' -and $text -match '\d')
    $text
}

function Save-UserVariable {
    param([string]$Name, [string]$Value)

    [Environment]::SetEnvironmentVariable($Name, $Value, 'User')
    Set-Item -Path "Env:$Name" -Value $Value
}

function Get-SavedVariable {
    param([string]$Name)
    [Environment]::GetEnvironmentVariable($Name, 'User')
}

function Write-Id {
    param([string]$Label, [string]$Value)

    $line = "QM_CREATED $Label $Value"
    Write-Host $line
    Add-Content -Path $script:IdLog -Value $line
}

$resultsDir = Join-Path $PSScriptRoot 'results'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$IdLog = Join-Path $resultsDir "created-ids-setup-$stamp.log"

Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"

$health = Invoke-Api -Method GET -Path '/api/health'
if ($health.Status -ne 200 -or $health.Json.database -ne 'up') {
    throw "Health check failed (HTTP $($health.Status)). Stopping before any change."
}
Write-Host 'Health check: database up.'

# --- Staff -------------------------------------------------------------------------------
$staffUser = Get-SavedVariable 'QM_STAFF_USER'
$staffPassword = Get-SavedVariable 'QM_STAFF_PASSWORD'
$staffToken = $null

if ($staffUser -and $staffPassword) {
    $login = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $staffUser; password = $staffPassword }
    if ($login.Status -ne 200) {
        throw "Saved staff login does not work (HTTP $($login.Status)). Not changing anything. Check the account by hand."
    }
    $staffToken = $login.Json.access_token
    Write-Host 'Staff account: already set up, login works. Skipped.'
}
else {
    $adminLogin = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{
        email = $env:QM_ADMIN_USER; password = $env:QM_ADMIN_PASSWORD
    }
    if ($adminLogin.Status -ne 200) { Stop-WithApiError 'Administrator login' $adminLogin }
    $adminToken = $adminLogin.Json.access_token

    $existing = Invoke-Api -Method GET -Path '/api/staff?search=qm-pt-staff&includeInactive=true&pageSize=50' -Token $adminToken
    if ($existing.Status -ne 200) { Stop-WithApiError 'Staff lookup' $existing }
    $match = @($existing.Json.items | Where-Object { $_.email -eq $StaffEmail })
    if ($match.Count -gt 0) {
        throw "$StaffEmail already exists but no saved password was found. Not resetting it automatically."
    }

    $newPassword = New-Password
    $create = Invoke-Api -Method POST -Path '/api/staff' -Token $adminToken -Body @{
        first_name         = 'qm-pt-staff'
        last_name          = 'QM Test'
        email              = $StaffEmail
        temporary_password = $newPassword
        role               = 'duty_manager'
        department         = 'QM Test'
    }
    if ($create.Status -ne 201) { Stop-WithApiError 'Create staff' $create }

    Save-UserVariable 'QM_STAFF_USER' $StaffEmail
    Save-UserVariable 'QM_STAFF_PASSWORD' $newPassword
    Write-Id 'staff' $create.Json.id

    $login = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $StaffEmail; password = $newPassword }
    if ($login.Status -ne 200) { Stop-WithApiError 'Staff login after create' $login }
    $staffToken = $login.Json.access_token
}

# --- Patient account ---------------------------------------------------------------------
$patientUser = Get-SavedVariable 'QM_PATIENT_USER'
$patientPassword = Get-SavedVariable 'QM_PATIENT_PASSWORD'
$patientToken = $null
$accountId = $null

if ($patientUser -and $patientPassword) {
    $login = Invoke-Api -Method POST -Path '/api/auth/patient/login' -Body @{ username = $patientUser; password = $patientPassword }
    if ($login.Status -ne 200) {
        throw "Saved patient login does not work (HTTP $($login.Status)). Not changing anything. Check the account by hand."
    }
    $patientToken = $login.Json.access_token
    $accountId = $login.Json.principal.id
    Write-Host 'Patient account: already set up, login works. Skipped.'
}
else {
    $newPassword = New-Password
    $register = Invoke-Api -Method POST -Path '/api/auth/patient/register' -Body @{
        username = $PatientUsername; password = $newPassword
    }
    if ($register.Status -eq 409) {
        throw "Patient username $PatientUsername already exists but no saved password was found. Not resetting it automatically."
    }
    if ($register.Status -ne 201) { Stop-WithApiError 'Register patient account' $register }

    Save-UserVariable 'QM_PATIENT_USER' $PatientUsername
    Save-UserVariable 'QM_PATIENT_PASSWORD' $newPassword
    $patientToken = $register.Json.access_token
    $accountId = $register.Json.principal.id
    Write-Id 'patient_account' $accountId
}

# --- Patient record and link -------------------------------------------------------------
$profile = Invoke-Api -Method GET -Path '/api/me/profile' -Token $patientToken
if ($profile.Status -eq 200) {
    Write-Host 'Patient record: already linked to the account. Skipped.'
}
else {
    $search = Invoke-Api -Method GET -Path "/api/patients?search=$([uri]::EscapeDataString($PatientRecordName))&pageSize=50" -Token $staffToken
    if ($search.Status -ne 200) { Stop-WithApiError 'Patient record lookup' $search }
    $record = @($search.Json.items | Where-Object { $_.full_name -eq $PatientRecordName }) | Select-Object -First 1

    if ($record) {
        $recordId = $record.id
        Write-Host 'Patient record: found the existing one, reusing it.'
    }
    else {
        $nic = '19' + (Get-Random -Minimum 50 -Maximum 99) + ('{0:D8}' -f (Get-Random -Minimum 0 -Maximum 99999999))
        $create = Invoke-Api -Method POST -Path '/api/patients' -Token $staffToken -Body @{
            full_name = $PatientRecordName; nic = $nic; gender = 'other'
        }
        if ($create.Status -ne 201) { Stop-WithApiError 'Create patient record' $create }
        $recordId = $create.Json.id
        Write-Id 'patient' $recordId
    }

    $link = Invoke-Api -Method POST -Path "/api/patients/$recordId/link-account" -Token $staffToken -Body @{ user_account_id = $accountId }
    if ($link.Status -ne 204) { Stop-WithApiError 'Link account to record' $link }

    $profile = Invoke-Api -Method GET -Path '/api/me/profile' -Token $patientToken
    if ($profile.Status -ne 200) { Stop-WithApiError 'Patient profile check after linking' $profile }
    Write-Host 'Patient record: linked, /me/profile works.'
}

Write-Host "Finished: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
Write-Host 'Logins are saved as Windows user variables (QM_STAFF_*, QM_PATIENT_*). No password was printed.'
Write-Host 'Ids (for cleanup) are in the results folder, in a created-ids-setup file.'
