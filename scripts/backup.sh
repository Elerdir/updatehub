#!/usr/bin/env bash
# backup.sh — backs up UpdateHub data directory (SQLite DB + artifacts)
#
# Usage:  ./scripts/backup.sh [DATA_DIR] [BACKUP_DIR]
# Defaults: DATA_DIR=/app/data  BACKUP_DIR=./backups
#
# Recommended cron (daily at 03:00):
#   0 3 * * * /opt/updatehub/scripts/backup.sh /app/data /var/backups/updatehub

set -euo pipefail

DATA_DIR="${1:-/app/data}"
BACKUP_DIR="${2:-./backups}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
ARCHIVE="${BACKUP_DIR}/updatehub_${TIMESTAMP}.tar.gz"

mkdir -p "$BACKUP_DIR"

echo "[updatehub-backup] $(date '+%Y-%m-%d %H:%M:%S') — backing up $DATA_DIR"

# SQLite hot backup: copy while DB may be open (WAL mode safe)
if [ -f "${DATA_DIR}/updatehub.db" ]; then
    sqlite3 "${DATA_DIR}/updatehub.db" ".backup '${DATA_DIR}/updatehub.db.bak'" 2>/dev/null \
        || cp "${DATA_DIR}/updatehub.db" "${DATA_DIR}/updatehub.db.bak"
fi

tar -czf "$ARCHIVE" -C "$(dirname "$DATA_DIR")" "$(basename "$DATA_DIR")"

# Remove the temp backup copy
rm -f "${DATA_DIR}/updatehub.db.bak"

SIZE=$(du -sh "$ARCHIVE" | cut -f1)
echo "[updatehub-backup] Done — $ARCHIVE ($SIZE)"

# Keep only the last 30 backups
find "$BACKUP_DIR" -name "updatehub_*.tar.gz" -type f \
    | sort -r | tail -n +31 | xargs -r rm --
