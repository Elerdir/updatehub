# UpdateHub

Self-hosted update server for desktop applications. Speaks **Tauri**,
**electron-updater**, **Sparkle**, **Velopack**, and a **generic JSON**
protocol so anything in any language can integrate with a single
`GET` request.

```
┌────────────┐   GET /api/apps/my-app/update?version=…    ┌───────────────┐
│ Your app   │ ────────────────────────────────────────►  │   UpdateHub   │
│            │ ◄────────────────────  download_url, sha   │     server    │
└────────────┘                                            └───────────────┘
                                                                  ▲
                                                                  │ POST artifacts
                                                                  │
                                                            ┌───────────────┐
                                                            │ your CI build │
                                                            └───────────────┘
```

## Features

- **Multi-format update endpoints** — Tauri, electron-updater (YAML),
  Sparkle (XML), Velopack (JSON), and a generic JSON for everything else
- **Stepping-stone upgrades** — `Release.MinFromVersion` lets you force
  clients on v1 to pass through v4 before reaching v6, transparently
- **Admin UI** — Blazor Server, light/dark/auto theme, EN / CS / DE
- **Multi-user with roles** — Admin / Manager / Viewer, full audit trail
- **2FA TOTP** with single-use backup codes; personal access tokens for CI
- **Forgot-password flow** with single-use email links
- **Webhook signing** — HMAC-SHA256 on outgoing publish webhooks
- **Analytics + storage management** — per-day download chart, per-app
  disk usage breakdown, bulk-cleanup of archived releases
- **Runtime SMTP config** — edit and test SMTP credentials from the UI
- **One-click backup** of database + encryption keys
- **Docker-ready** — single multi-platform image, single volume

## Quick start

```bash
# 1. Generate a bcrypt hash for your admin password
cd tools/GenerateHash && dotnet run -- "your-real-password"

# 2. Put it in docker-compose.yml (double every $ to $$ for compose), e.g.
#    UpdateHub__Admin__PasswordHash=$$2a$$12$$…

# 3. Start it
docker compose up -d
```

Open `http://localhost:8081` and sign in with the credentials you set.

## Documentation

| Document | For whom |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Developers — codebase map, layers, data flow |
| [docs/INTEGRATE.md](docs/INTEGRATE.md) | App developers — embedding UpdateHub into your application |
| [docs/ADMIN.md](docs/ADMIN.md) | Operators — deploy, configure, back up, upgrade |
| [docs/integration-tauri.md](docs/integration-tauri.md) · [docs/integration-avalonia.md](docs/integration-avalonia.md) | Quick framework-specific recipes |
| [docs/upload-release.example.yml](docs/upload-release.example.yml) | Drop-in GitHub Actions workflow for publishing |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

## Repository layout

```
src/
  UpdateHub.Domain/          ← entities, enums, no dependencies
  UpdateHub.Application/     ← business logic, services, interfaces
  UpdateHub.Infrastructure/  ← EF Core, storage, SMTP, webhooks, queue
  UpdateHub.Web/             ← Blazor Server + Minimal API + auth
sdk/
  UpdateHub.Client/          ← .NET client library for consuming apps
  UpdateHub.Cli/             ← `updatehub` command-line uploader
tests/
  UpdateHub.Application.Tests/  ← service unit tests (NSubstitute)
  UpdateHub.Web.Tests/          ← integration tests via WebApplicationFactory
tools/
  GenerateHash/              ← bcrypt hash helper
docs/                        ← guides & examples
```

## License

MIT.
