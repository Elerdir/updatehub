# UpdateHub

Self-hosted update server for all your desktop applications. Supports Tauri (built-in updater protocol) and a generic JSON protocol for any other app (Avalonia/.NET, Java, etc.).

## Features

- **Tauri updater** — serves the exact manifest format Tauri expects
- **Generic JSON** — universal update check endpoint for any app
- **Admin UI** — web interface for managing apps, releases and artifacts
- **CI/CD upload** — upload artifacts directly from GitHub Actions
- **No external dependencies** — SQLite database, local file storage
- **Docker-ready** — single container, data mounted as a volume

## Quick Start (Docker)

```bash
# 1. Generate a bcrypt hash for your password (e.g. at https://bcrypt.online/)
# 2. Edit docker-compose.yml and fill in the env variables
# 3. Run:
docker compose up -d
```

Admin UI: `http://localhost:8080`

Default dev credentials: `admin` / `admin123`

## Manual Setup

```bash
# Requires .NET 10 SDK
cd src/UpdateHub.Web
dotnet run
```

Access at `http://localhost:5000` — uses `appsettings.Development.json` automatically.

## Configuration

All configuration is via environment variables (or `appsettings.Production.json`):

| Variable | Description | Default |
|---|---|---|
| `UpdateHub__BaseUrl` | Public URL of your server (used in download links) | — |
| `UpdateHub__DatabasePath` | Path to SQLite database file | `updatehub.db` |
| `UpdateHub__StoragePath` | Directory where artifacts are stored | `artifacts` |
| `UpdateHub__CiToken` | Secret token for CI/CD uploads | — |
| `UpdateHub__Admin__Username` | Admin login username | `admin` |
| `UpdateHub__Admin__PasswordHash` | BCrypt hash of admin password | — |

### Generating a password hash

Use any BCrypt tool, e.g. online at https://bcrypt.online/ with cost factor 12.

Or install a small .NET script:
```bash
dotnet script -e "Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(\"yourpassword\", 12));"
```

## API

### Public endpoints (no auth)

#### Generic JSON update check
```
GET /api/apps/{slug}/update?version=1.0.0&platform=windows&arch=x64
```

Response:
```json
{
  "has_update": true,
  "version": "1.1.0",
  "release_notes": "Bug fixes",
  "download_url": "https://yourserver/api/downloads/...",
  "sha256": "abc123...",
  "is_mandatory": false,
  "channel": "stable"
}
```

Parameters:
- `version` — current app version (required)
- `platform` — `windows`, `macos`, `linux`
- `arch` — `x64`, `arm64`, `x86`
- `channel` — `stable` (default), `beta`, `alpha`

#### Tauri updater manifest
```
GET /api/apps/{slug}/tauri/latest.json
```

Returns the standard Tauri updater JSON format.

#### Download artifact
```
GET /api/downloads/{artifactId}
```

### CI/CD endpoint (token auth)

Upload a release artifact from your build pipeline:

```bash
curl -X POST "https://yourserver/api/ci/apps/{slug}/releases" \
  -H "X-UpdateHub-Token: YOUR_CI_TOKEN" \
  -F "file=@./dist/MyApp-Setup.exe" \
  -F "version=1.1.0" \
  -F "platform=windows" \
  -F "arch=x64" \
  -F "channel=stable" \
  -F "release_notes=Bug fixes and improvements"
```

The artifact is uploaded as a **Draft** release. Go to the admin UI to publish it.

Optional fields:
- `signature` — Tauri Ed25519 signature (content of the `.sig` file)
- `is_mandatory` — `true` if this update should be forced

## Integrating your apps

### Avalonia / .NET apps

```csharp
var response = await httpClient.GetFromJsonAsync<UpdateCheckResponse>(
    $"https://yourserver/api/apps/my-app/update?version={currentVersion}&platform=windows&arch=x64");

if (response?.HasUpdate == true)
{
    // Show update dialog to user
    // response.DownloadUrl, response.Version, response.ReleaseNotes
}
```

### Tauri apps

In `tauri.conf.json`:
```json
{
  "updater": {
    "active": true,
    "endpoints": ["https://yourserver/api/apps/my-app/tauri/latest.json"],
    "dialog": true
  }
}
```

### GitHub Actions upload example

See [`.github/workflows/upload-release.yml`](.github/workflows/upload-release.yml) for a complete example.

Required secrets in your app repo:
- `UPDATEHUB_URL` — your server URL
- `UPDATEHUB_CI_TOKEN` — your CI token

Required variables:
- `APP_SLUG` — the app slug as registered in UpdateHub

## Production deployment

1. Set up a server (VPS or home server)
2. Install Docker
3. Clone this repo
4. Create `docker-compose.override.yml` with your env vars
5. Set up nginx as a reverse proxy with HTTPS (Let's Encrypt)
6. Run `docker compose up -d`

See [ARCHITECTURE.md](ARCHITECTURE.md) for more detail.
