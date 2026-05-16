# Changelog

All notable changes to UpdateHub are documented here.

## [1.1.0] — 2026-05-14

### Added
- **Two-factor authentication (TOTP)** — optional 2FA for the admin account; setup via the Settings page (QR/`otpauth://` URI), verified through a dedicated `TwoFactorPending` cookie scheme and a `/login/totp` step
- **Audit log** — every admin and CI action (app/release/artifact CRUD, publish, login success/failure, token rotation, IP blocks) is recorded with actor, IP, and timestamp; viewable on the `/audit` page
- **EF Core migrations** — schema is now managed by proper migrations (`Database.Migrate()`) instead of `EnsureCreated()`, enabling clean upgrades
- **Email / SMTP notifications** — optional MailKit-based alerts for release publish, IP blocks, and password changes; configured via `UpdateHub:Smtp:*`
- **Password change UI** — admin can change the password from the Settings page; the new bcrypt hash is stored in the database and overrides `appsettings.json`
- **Per-app CI tokens** — each app can have its own upload token (rotate/reveal/remove on the app detail page); falls back to the global token
- **Serilog logging** — structured logging to console and daily rolling files (`UpdateHub:Serilog`); separate Production config
- **Extended health checks** — `/health` reports database connectivity and free disk space as a rich JSON document
- **Login rate limiting** — dedicated sliding-window limiter on the login and TOTP endpoints (10 attempts / 5 min per IP)
- **Content-Security-Policy header** — CSP added alongside the existing security headers, scoped to allow Blazor SignalR
- **Backup scripts** — helper scripts for backing up the SQLite database and artifacts
- **Download counter** — each artifact tracks `DownloadCount`; incremented atomically on every download; displayed on the app detail page and in the dashboard stats
- **Markdown release notes** — `MarkdownView` Blazor component (Markdig) renders formatted release notes on release detail pages
- **CI token rotation** — Settings page allows revealing the current CI token and generating a new one; token is stored in the database so it survives restarts without touching `appsettings.json`
- **Dockerfile HEALTHCHECK** — `GET /health` endpoint; Docker marks the container healthy/unhealthy automatically
- **OpenAPI + Scalar UI** — full API schema at `/api/docs` (Scalar interactive explorer); machine-readable spec at `/openapi/v1.json`
- **Webhook on publish** — when a release is published, UpdateHub POSTs a JSON payload to `UpdateHub:WebhookUrl` (if configured); failure is non-fatal and never blocks the publish flow
- **Brute-force protection** — failed login attempts are tracked per IP; auto-block after 5 failures; records first/last attempt time and user agent
- **Security page** — admin UI for viewing all login attempts with block/unblock/delete actions; filterable by IP; shows failed count, first/last seen, user agent, and manual-vs-auto block distinction
- **Rate limiting** — sliding-window 60 req/min per IP on public update-check, Tauri manifest, and download endpoints; returns `429 Too Many Requests`
- **Copy-to-clipboard buttons** — app detail page shows computed update-check and Tauri manifest URLs with one-click copy via `navigator.clipboard`
- **Settings + Security nav links** — both pages added to the sidebar navigation
- **Security HTTP headers** — `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy` added to every response via middleware
- **Forwarded-headers support** — `UseForwardedHeaders` with `XForwardedFor + XForwardedProto` so real client IPs are detected correctly behind nginx/Caddy
- **Login blocked message** — `/login?error=blocked` displays a dedicated message when an IP is auto/manually blocked, separate from the wrong-password error
- **`global.json`** — pins .NET SDK to 10.0.x (`rollForward: latestMinor`) so any compatible SDK in the 10.0 family works
- **`.gitattributes`** — enforces LF line endings across the repo; Windows `.bat`/`.cmd`/`.ps1` files keep CRLF
- **CI/CD pipeline** — GitHub Actions: unit + integration tests on every push; multi-platform (amd64 + arm64) Docker image built and pushed to GHCR on push to `main` and on version tags
- **Integration tests** — `UpdateHub.Web.Tests` uses `WebApplicationFactory` with SQLite shared-cache in-memory; covers public API happy paths and error cases
- **Unit tests** — `UpdateHub.Application.Tests` covers `AdminService`, `UpdateResolverService`, and `BruteForceProtectionService` with NSubstitute mocks

### Security
- **Release-notes XSS hardened** — `MarkdownView` disables raw HTML in Markdig, so release notes (settable via the token-authenticated CI endpoint) cannot inject `<script>` into the admin UI
- **TOTP secret encrypted at rest** — the 2FA shared secret is protected via ASP.NET Core Data Protection (`ISecretProtector`); keys are persisted next to the database so they survive container restarts; a plaintext fallback keeps pre-existing 2FA setups working
- **CI uploads audited** — token-based uploads go through `AdminService.IngestCiUploadAsync` and are recorded in the audit log (actor `ci`)
- **`System.Security.Cryptography.Xml` pinned to 9.0.16** — resolves NU1903 high-severity advisories from a transitive Data Protection dependency

### Changed
- **Streaming artifact uploads** — `LocalArtifactStorage` streams uploads straight to disk while hashing instead of buffering the whole file in memory (large installers no longer consume equivalent RAM)
- **Real semver version comparison** — `UpdateResolverService` uses the `Semver` package; pre-release tags are ordered correctly (`1.0.0-beta` < `1.0.0`, `beta.2` < `beta.10`)
- **Non-blocking notifications** — publish webhooks/emails and brute-force block emails run on a background queue (`BackgroundNotificationQueue`); requests no longer block on SMTP or webhook latency
- **Auth endpoints extracted** — login/TOTP/logout handlers moved out of `Program.cs` into `AuthEndpoints.cs`
- `AdminService` app lookups (`Update`/`Delete`/`RotateCiToken`/`ClearCiToken`) use a dedicated `GetByIdAsync` instead of loading every app with all releases
- `GetStatsAsync` now returns a 3-tuple `(apps, published, downloads)` — dashboard shows total lifetime download count
- Publishing a release automatically archives any older published releases for the same app + channel
- CI token fallback: `SettingsService` reads from the database only; the Web layer seeds the value from `appsettings.json` on first run if the DB has no token yet

### Fixed
- `AdminService.UpdateAppAsync` could throw `NullReferenceException` for a missing app — the previous `ContinueWith`/`??` chain made the null check dead code
- `CiEndpoints` issued a duplicate `GetBySlugAsync` query per upload
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
