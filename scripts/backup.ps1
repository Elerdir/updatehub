# backup.ps1 — backs up UpdateHub data directory (SQLite DB + artifacts)
#
# Usage:  .\scripts\backup.ps1 [-DataDir <path>] [-BackupDir <path>]
# Defaults: DataDir=.\data  BackupDir=.\backups
#
# Scheduled task (daily at 03:00):
#   schtasks /create /tn "UpdateHub Backup" /tr "powershell -File C:\updatehub\scripts\backup.ps1" /sc daily /st 03:00

param(
    [string]$DataDir   = ".\data",
    [string]$BackupDir = ".\backups"
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$archive   = Join-Path $BackupDir "updatehub_$timestamp.zip"

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

Write-Host "[updatehub-backup] $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') — backing up $DataDir"

$dbPath    = Join-Path $DataDir "updatehub.db"
$dbBakPath = Join-Path $DataDir "updatehub.db.bak"

# Copy DB file while it may be open (SQLite WAL mode is safe to copy)
if (Test-Path $dbPath) {
    Copy-Item -Path $dbPath -Destination $dbBakPath -Force
}

Compress-Archive -Path $DataDir -DestinationPath $archive -CompressionLevel Optimal

# Remove temp copy
if (Test-Path $dbBakPath) { Remove-Item $dbBakPath -Force }

$size = (Get-Item $archive).Length / 1MB
Write-Host ("[updatehub-backup] Done — $archive ({0:F1} MB)" -f $size)

# Keep only the last 30 backups
$old = Get-ChildItem -Path $BackupDir -Filter "updatehub_*.zip" |
       Sort-Object LastWriteTime -Descending |
       Select-Object -Skip 30

foreach ($f in $old) {
    Remove-Item $f.FullName -Force
    Write-Host "[updatehub-backup] Removed old backup: $($f.Name)"
}
