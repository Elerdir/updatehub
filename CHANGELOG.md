# Changelog

All notable changes to UpdateHub are documented here.

## [Unreleased]

### Added
- **Download counter** — each artifact tracks `DownloadCount`; incremented atomically on every download; displayed on the app detail page and in the dashboard stats
- **Markdown release notes** — `MarkdownView` Blazor component (Markdig) renders formatted release notes on release detail pages
- **CI token rotation** — Settings page allows revealing the current CI token and generating a new one; token is stored in the database so it survives restarts without touching `appsettings.json`
- **Dockerfile HEALTHCHECK** — `GET /health` endpoint; Docker marks the container healthy/unhealthy automatically
- **OpenAPI + Scalar UI** — full API schema at `/api/docs` (Scalar interactive explorer); machine-readable spec at `/openapi/v1.json`
- **Webhook on publish** — when a release is published, UpdateHub POSTs a JSON payload to `UpdateHub:WebhookUrl` (if configured); failure is non-fatal and never blocks the publish flow
- **Brute-force protection** — failed login attempts are tracked per IP; auto-block after 5 failures; records first/last attempt time and user agent
- **Security page** — admin UI for viewing all login attempts with block/unblock/delete actions; filterable by IP; shows failed count, first/last seen, user agent, and manual-vs-auto block distinction
- **Rate limiting** — sliding-window 60 req/min per IP on public update-check and Tauri manifest endpoints; returns `429 Too Many Requests`
- **Copy-to-clipboard buttons** — app detail page shows computed update-check and Tauri manifest URLs with one-click copy via `navigator.clipboard`
- **Settings + Security nav links** — both pages added to the sidebar navigation
- **Security HTTP headers** — `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy` added to every response via middleware
- **Forwarded-headers support** — `UseForwardedHeaders` with `XForwardedFor + XForwardedProto` so real client IPs are detected correctly behind nginx/Caddy
- **Login blocked message** — `/login?error=blocked` displays a dedicated message when an IP is auto/manually blocked, separate from the wrong-password error
- **`global.json`** — pins .NET SDK to 10.0.x (`rollForward: latestMinor`) so any compatible SDK in the 10.0 family works
- **`.gitattributes`** — enforces LF line endings across the repo; Windows `.bat`/`.cmd`/`.ps1` files keep CRLF
- **CI/CD pipeline** — GitHub Actions: unit + integration tests on every push; Docker image built and pushed to GHCR on push to `main`
- **Integration tests** — `UpdateHub.Web.Tests` uses `WebApplicationFactory` with SQLite shared-cache in-memory; covers public API happy paths and error cases
- **Unit tests** — `UpdateHub.Application.Tests` covers `AdminService` and `BruteForceProtectionService` with NSubstitute mocks

### Changed
- `GetStatsAsync` now returns a 3-tuple `(apps, published, downloads)` — dashboard shows total lifetime download count
- Publishing a release automatically archives any older published releases for the same app + channel
- CI token fallback: `SettingsService` reads from the database only; the Web layer seeds the value from `appsettings.json` on first run if the DB has no token yet

### Fixed
- `RemoteIpAddress` always returning `127.0.0.1` behind a reverse proxy — fixed by enabling `UseForwardedHeaders`

## [1.0.0] — 2026-05-09

### Added
- Initial release
- ASP.NET Core 10 + Blazor Server admin UI
- Generic JSON update check endpoint (`GET /api/apps/{slug}/update`)
- Tauri updater manifest endpoint (`GET /api/apps/{slug}/tauri/latest.json`)
- Artifact download endpoint (`GET /api/downloads/{id}`)
- CI/CD upload endpoint (`POST /api/ci/apps/{slug}/releases`)
- Application management (create, edit, delete)
- Release management (draft → published → archived)
- Artifact upload with automatic SHA-256 hashing
- Release channels: Stable, Beta, Alpha
- Mandatory update flag
- Cookie-based admin authentication
- SQLite database with EF Core
- Local filesystem artifact storage
- Docker + Docker Compose support
- GitHub Actions CI workflow
- GitHub Actions release upload example workflow
