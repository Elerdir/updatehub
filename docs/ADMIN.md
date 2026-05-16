# Administrator's guide

Operating UpdateHub: install, configure, run, back up, upgrade.

## 1. Deploy

The supported deployment is the official Docker image.

```yaml
# docker-compose.yml — minimal example
services:
  updatehub:
    image: ghcr.io/elerdir/updatehub:latest
    ports:
      - "8081:8080"
    volumes:
      - ./data:/app/data
      - ./logs:/app/logs
    environment:
      - UpdateHub__BaseUrl=https://updates.example.com
      - UpdateHub__Admin__Username=admin
      - UpdateHub__Admin__PasswordHash=$$2a$$12$$…           # bcrypt — see § 2
      - UpdateHub__CiToken=$$RANDOM$$                          # 32+ char random
    restart: unless-stopped
```

On first start UpdateHub will:

1. Run EF Core migrations and create the SQLite database under `/app/data`
2. Generate a Data Protection key ring under `/app/data/dp-keys` (this is
   what encrypts TOTP secrets, SMTP passwords, and the webhook signing key)
3. Seed the admin user from `UpdateHub__Admin__*` config — *only* if the
   `Users` table is empty
4. Start listening on port 8080 inside the container

Put nginx / Caddy in front for TLS and forward to the `8081` host port.

## 2. The bootstrap admin password

`UpdateHub__Admin__PasswordHash` must be a bcrypt hash. Generate one of
two ways:

```bash
# From the cloned repo:
cd tools/GenerateHash
dotnet run -- "your-real-password"
```

```bash
# Or use any bcrypt CLI / online tool. Cost factor 12 is what UpdateHub
# uses internally; anything between 10 and 14 is fine.
```

Bcrypt hashes contain `$` characters. In `docker-compose.yml` you must
double them (`$$2a$$12$$…`) so Compose does not try to interpolate
variables. In a `.env` file the value is taken literally — no doubling.

Once a real user exists in the database, the `UpdateHub__Admin__*`
config is **not** consulted at login. To rotate the admin password
later, use the in-product "Change password" flow on `/account`.

## 3. Configuration

Every setting comes from either an environment variable or an
`appsettings.*.json` file. ASP.NET Core merges them; env wins.

| Key (env / JSON path) | What it does | Default |
|---|---|---|
| `UpdateHub__BaseUrl` | Public URL used in download links and password-reset emails | empty |
| `UpdateHub__DatabasePath` | SQLite file path inside the container | `updatehub.db` (in `WORKDIR`) |
| `UpdateHub__StoragePath` | Directory where artifacts are stored | `artifacts` |
| `UpdateHub__DataProtectionKeysPath` | Where the encryption key ring is persisted | `<dbDir>/dp-keys` |
| `UpdateHub__CiToken` | Initial global CI token (overridden once you rotate it in the UI) | empty |
| `UpdateHub__WebhookUrl` | Global webhook fired on publish (per-app URLs override this) | empty |
| `UpdateHub__Smtp__Host` / `Port` / `From` / `Username` / `Password` / `To` | Initial SMTP credentials (override via /settings at runtime) | empty |
| `UpdateHub__Admin__Username` / `PasswordHash` | Bootstrap admin credentials | `admin` / *(no hash)* |

The official Docker image sets `UpdateHub__DatabasePath=/app/data/updatehub.db`
and `UpdateHub__StoragePath=/app/data/artifacts` so a single `./data:/app/data`
volume holds **everything that survives a restart**: DB, artifacts, and
the Data Protection key ring.

### Runtime overrides

The following config is editable through the admin UI and stored in the
SQLite `Settings` table. Once set, the DB value wins over the env value:

- CI token (Settings → CI Upload Token → Rotate)
- SMTP credentials (Settings → Email (SMTP))
- Webhook signing secret (Settings → Webhook signing)
- Admin password (My Account → Change password)
- 2FA TOTP secret and backup codes (My Account)

This means you can deploy with placeholder env values and complete the
configuration through the UI without touching the host.

## 4. Users, roles and 2FA

| Role | Can do |
|---|---|
| **Admin** | Everything — manage users, edit settings, block IPs, delete apps |
| **Manager** | CRUD on apps + releases + artifacts, publish, archive. *Cannot* manage users, edit settings, or block IPs. |
| **Viewer** | Read everything (apps, releases, audit, security, analytics, storage). No write actions appear in the UI. |

Admin creates new users on `/users`. Workflow:

1. *+ New user* → fill username, role, optional email
2. Click **Generate** for a strong temporary password (you can also type
   your own)
3. Save — the temporary password is shown **once** alongside a Copy
   button. After that, only the bcrypt hash is in the database. The
   admin must hand the temp password to the user via a side channel.
4. On first login the user is forced into `/account/change-password`
   before they can use anything else.

If the user forgets their password and has set up an email in their
profile, they can use `/account/forgot` — a single-use, 30-minute link
is mailed to them. Otherwise an admin resets the password on `/users`
(same flow as creation: pick / generate, copy once, hand over).

### Two-factor authentication

Each user can enable TOTP from their My Account page. UpdateHub
generates 10 **single-use backup codes** at the moment 2FA is turned
on and shows them once. If the user loses their authenticator, any
unused backup code substitutes for a TOTP code at login. Codes can be
regenerated at any time — that invalidates the previous batch.

The TOTP secret is encrypted at rest with ASP.NET Core Data Protection;
the key ring lives in `/app/data/dp-keys`. **Back up that directory
together with the database** — without it, every encrypted secret
becomes unrecoverable.

### Session revocation

Each user has a SecurityStamp. Anything that changes their security
posture (password change, admin reset, role change, deactivate, "Sign
out everywhere" on My Account) rotates the stamp. The next request
from an existing cookie compares stamps and signs the user out if they
don't match. This is the instant-revocation primitive UpdateHub uses
in place of a server-side session table.

## 5. Security knobs

- **Login rate-limit**: 10 attempts per 5 minutes per IP. After 5
  consecutive failures the IP is auto-blocked. See `/security`.
- **Public API rate-limit**: 60 requests per minute per IP on update
  checks, manifests, and downloads.
- **Webhook signing**: when a secret is configured on `/settings`,
  every outgoing publish webhook carries an
  `X-UpdateHub-Signature: sha256=…` header — HMAC-SHA256 of the JSON
  body. Receivers should verify and reject mismatches.
- **CSP / security headers**: enabled by default on every response —
  no `unsafe-eval`, no inline event handlers, `frame-ancestors 'none'`.
- **Data Protection** encrypts TOTP secrets, SMTP passwords, the
  webhook secret, and the personal access token hash storage marker.

## 6. Backups

The admin UI exposes a one-click backup on Settings →
**Download backup**:

- The ZIP contains `updatehub.db` (with its `-shm`/`-wal` companions),
  the full `dp-keys/` directory, and a small `README.txt`
- **Artifact files are intentionally excluded** — they're huge and
  easy to back up at the volume level (e.g. `rsync` the `data/`
  directory, or snapshot the underlying disk)

A complete restore is therefore:

```bash
# 1. Stop the container
docker compose down

# 2. Replace the metadata
unzip updatehub-backup-…zip -d ./data

# 3. Bring back the artifact files from your separate backup
rsync -a artifacts-backup/ ./data/artifacts/

# 4. Restart
docker compose up -d
```

Schedule it: a cron job that hits `GET /admin/backup.zip` with the
admin's cookie or via a small admin script. Anything more involved
than a daily ZIP is overkill for a server at this scale.

## 7. Day-to-day operations

| Task | Where |
|---|---|
| Register a new application | Applications → + New Application |
| Rotate the global CI token | Settings → CI Upload Token → Rotate |
| Give an app its own CI token | Application detail → Per-app CI Token → Generate |
| Wire a Slack/Discord notification | Settings → Webhook signing (secret) + the per-app *Webhook URL* on edit |
| Watch what's happening | Dashboard recent-activity feed + `/audit` |
| Investigate slow / failing logins | `/security` |
| See how many people are running each version | `/analytics` |
| Clean up old artifacts | `/storage` — bulk delete archived releases older than N days |

## 8. Upgrading

```bash
docker compose pull
docker compose up -d
```

The container runs migrations on every start; the upgrade is in-place.
Take a backup beforehand (Settings → Download backup) if the
migration list contains anything you don't recognise.

The `latest` tag tracks `main`. For predictable upgrades pin to a
specific tag (`ghcr.io/elerdir/updatehub:1.1.0` etc.) and bump it
deliberately.

## 9. Health monitoring

`GET /health` returns a rich JSON document covering the database
connectivity check and free disk space on the artifact volume. Wire
it into your monitoring of choice — the Docker container already
declares it as the `HEALTHCHECK`.

## 10. Troubleshooting

- **Bootstrap admin doesn't appear** — the seeder requires
  `UpdateHub__Admin__PasswordHash` to be present and the `Users`
  table to be empty. Check the container logs for the
  "Bootstrap admin … seeded" line.
- **"Invalid username or password" with a hash you just generated**
  — make sure the `$` in the bcrypt hash is doubled to `$$` if you
  pasted it directly into `docker-compose.yml`. In a `.env` file it
  is literal.
- **TOTP code rejected after restoring a backup** — the Data
  Protection key ring (`dp-keys/`) must be restored *together*
  with the database. Without the matching key, encrypted secrets
  cannot be unsealed.
- **Webhook receiver rejects signature** — verify it canonicalises
  the payload exactly as UpdateHub sends it: raw bytes, no
  re-serialisation. The HMAC is `sha256=` followed by 64 hex chars.
- **Container is healthy but `/health` reports `Unhealthy` for
  disk_space** — that means free space on the artifact volume is
  below 1 GB. Use the Storage page to bulk-archive or expand the
  volume.

## 11. What lives where on disk

Inside the container (under `/app/data` thanks to the Docker volume):

```
data/
├── updatehub.db, .db-shm, .db-wal   ← SQLite + WAL companions
├── dp-keys/                          ← Data Protection key ring
└── artifacts/
    └── <app-slug>/<version>/<filename>
```

Backups should always include `updatehub.db*` *and* `dp-keys/*`.
Artifact directories are pure file storage and can be backed up
independently with whatever block / object-storage strategy fits.
