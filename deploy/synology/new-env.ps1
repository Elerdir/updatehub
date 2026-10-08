<#
  Generates deploy/synology/.env for the Synology deployment:
  bcrypt hash of the admin password (prompted, never echoed) + random CI token.
  Usage:  powershell -ExecutionPolicy Bypass -File deploy\synology\new-env.ps1
#>
param(
    [string]$BaseUrl  = 'https://updatehub.niderle.cz',
    [string]$Username = 'admin'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$envPath  = Join-Path $PSScriptRoot '.env'

if (Test-Path $envPath) {
    $answer = Read-Host ".env already exists. Overwrite? (y/N)"
    if ($answer -ne 'y') { Write-Host 'Aborted.'; exit 1 }
}

$secure  = Read-Host 'Admin password' -AsSecureString
$confirm = Read-Host 'Admin password (again)' -AsSecureString
$plain   = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
$plain2  = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirm))
if ($plain -ne $plain2)   { throw 'Passwords do not match.' }
if ($plain.Length -lt 12) { throw 'Use at least 12 characters.' }

$output = & dotnet run --project (Join-Path $repoRoot 'tools\GenerateHash') -- $plain
$hash = $output | Where-Object { $_ -match '^\$2[aby]\$' } | Select-Object -First 1
if (-not $hash) { throw "Hash generation failed:`n$output" }

$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$ciToken = ([BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()

# Single quotes keep compose from interpolating the $ signs in the bcrypt hash.
$content = @(
    "UpdateHub__BaseUrl='$BaseUrl'"
    "UpdateHub__Admin__Username='$Username'"
    "UpdateHub__Admin__PasswordHash='$hash'"
    "UpdateHub__CiToken='$ciToken'"
) -join "`n"
[IO.File]::WriteAllText($envPath, $content + "`n", (New-Object Text.UTF8Encoding($false)))

Write-Host "Written $envPath"
Write-Host "CI token (also in .env, needed for GitHub Actions secret UPDATEHUB_TOKEN):"
Write-Host $ciToken
