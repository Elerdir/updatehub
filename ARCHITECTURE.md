# Architecture

UpdateHub follows **Clean Architecture**: dependencies point inward, inner
layers never reference outer layers. There's a single solution with five
projects plus a CLI:

```
┌──────────────────────────────────────────────────────────────────┐
│                         UpdateHub.Web                            │
│              ASP.NET Core 10  +  Blazor Server                   │
│   (HTTP endpoints, Razor pages, auth, localization, theming)     │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                  UpdateHub.Infrastructure                  │  │
│  │   EF Core (SQLite), MailKit (SMTP), Data Protection,       │  │
│  │   IArtifactStorage (filesystem), webhook HMAC signer,      │  │
│  │   BackgroundNotificationQueue                              │  │
│  │  ┌──────────────────────────────────────────────────────┐  │  │
│  │  │                UpdateHub.Application                 │  │  │
│  │  │   AdminService · UserService · SettingsService ·     │  │  │
│  │  │   UpdateResolverService · BruteForceProtectionService│  │  │
│  │  │   EmailNotificationService · AuditService            │  │  │
│  │  │   + every Interface and Model used across the app    │  │  │
│  │  │  ┌────────────────────────────────────────────────┐  │  │  │
│  │  │  │              UpdateHub.Domain                  │  │  │  │
│  │  │  │   Entities + Enums.  No NuGet dependencies.    │  │  │  │
│  │  │  └────────────────────────────────────────────────┘  │  │  │
│  │  └──────────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘

  sdk/UpdateHub.Client   — client library a consuming app embeds
  sdk/UpdateHub.Cli      — `updatehub upload …` CLI for build pipelines
  tools/GenerateHash     — bcrypt hash helper for admin password seeding
```

The single rule: **referencing flows in only one direction**. Web sees
Infrastructure (to compose DI), Infrastructure sees Application, Application
sees Domain. Domain sees nothing. Razor pages and Minimal-API endpoints
inject **Application services**, never `AppDbContext` or `LocalArtifactStorage`.

## Where to find things

### `src/UpdateHub.Domain/`

Pure entities — what the system *is*, with no behaviour.

| File | What it represents |
|---|---|
| `Entities/App.cs` | A registered application (unique `Slug`, optional `WebhookUrl`, optional `CiToken`) |
| `Entities/Release.cs` | A version of an app in some channel; can carry `MinFromVersion` to gate the upgrade path |
| `Entities/Artifact.cs` | A single installer file (one Release can have many — different platforms / multiple formats per platform) |
| `Entities/AppSetting.cs` | Key/value row in the `Settings` table — runtime configuration (CI token, SMTP creds, webhook secret) |
| `Entities/User.cs` | Local account (`Role`, `MustChangePassword`, `SecurityStamp`, `TotpSecret`, `BackupCodes`, …) |
| `Entities/PasswordResetToken.cs` | One-shot, hashed, 30-minute reset link record |
| `Entities/PersonalAccessToken.cs` | Long-lived bearer token bound to a user |
| `Entities/LoginAttempt.cs` | Per-IP brute-force tracker |
| `Entities/AuditEntry.cs` | Append-only record of every meaningful action |
| `Entities/DownloadEvent.cs` | One row per artifact download — feeds the analytics page |
| `Enums/ReleaseChannel.cs` | Stable / Beta / Alpha |
| `Enums/ReleaseStatus.cs` | Draft / Published / Archived |
| `Enums/UserRole.cs` | Admin / Manager / Viewer |

### `src/UpdateHub.Application/`

Business logic that depends only on Domain. Talks to Infrastructure through
interfaces, never concrete types.

```
Application/
├── Interfaces/                           — contracts implemented by Infrastructure
│   ├── IAppRepository.cs · IReleaseRepository.cs · IArtifactRepository.cs
│   ├── IUserRepository.cs · IPasswordResetTokenRepository.cs
│   ├── IPersonalAccessTokenRepository.cs · ILoginAttemptRepository.cs
│   ├── ISettingsRepository.cs · IAuditRepository.cs · IDownloadEventRepository.cs
│   ├── IArtifactStorage.cs               — file storage abstraction
│   ├── IEmailService.cs · IWebhookService.cs
│   ├── INotificationQueue.cs             — background queue contract
│   ├── ISecretProtector.cs               — encrypt-at-rest abstraction
│   └── ICurrentUser.cs                   — request-scoped principal accessor
├── Authorization/
│   └── RoleGuard.cs                      — Require(currentUser, "Admin", …)
├── Services/
│   ├── AdminService.cs                   — apps / releases / artifacts CRUD
│   ├── UserService.cs                    — users, password reset, PAT, 2FA, backup codes
│   ├── SettingsService.cs                — DB-backed runtime config
│   ├── UpdateResolverService.cs          — picks the right release for a checking client
│   ├── BruteForceProtectionService.cs    — IP rate / blocking
│   ├── AuditService.cs                   — write/read audit log
│   ├── EmailNotificationService.cs       — domain-specific message templates
│   └── BaseUrlAccessor.cs                — DI-friendly carrier for the public URL
└── Models/
    ├── UpdateCheckResult.cs · TauriManifest.cs · SmtpConfig.cs
```

The `UpdateResolverService.PickBestForVersion` helper is the heart of the
**stepping-stone upgrade-path** logic — sort published releases by SemVer
precedence, skip the ones the caller can't directly install
(`MinFromVersion`), return the highest that remains.

### `src/UpdateHub.Infrastructure/`

Concrete implementations of every Application interface, EF Core DbContext,
storage, SMTP, webhook signing, and the background notification queue.

```
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs                   — DbSets, indexes, FK rules
│   ├── Migrations/                       — every schema change since 1.0
│   └── Repositories/                     — one file per repository
├── Storage/LocalArtifactStorage.cs       — streams uploads straight to disk
├── Email/SmtpEmailService.cs             — MailKit, reads DB config with env fallback
├── Security/DataProtectionSecretProtector.cs
├── BackgroundNotificationQueue.cs        — Channel-based queue with per-work DI scope
├── WebhookService.cs                     — HMAC-SHA256 signing, per-app URL routing
└── DependencyInjection.cs                — AddInfrastructure() — the ONLY composition root
```

`DependencyInjection.cs` is the only place that wires interfaces to
concrete types. Swapping `LocalArtifactStorage` for an S3 implementation
means changing one line here.

### `src/UpdateHub.Web/`

Presentation. Razor Server pages + Minimal API endpoints + auth glue.

```
Web/
├── Program.cs                            — bootstrap order: Serilog → DI → request pipeline
├── Endpoints/
│   ├── PublicEndpoints.cs                — GET /api/apps/* + /api/downloads/* + manifest formats
│   ├── CiEndpoints.cs                    — POST /api/ci/apps/{slug}/releases
│   ├── AuthEndpoints.cs                  — login / TOTP / forgot / reset / change-password / logout
│   └── AdminEndpoints.cs                 — /audit/export.csv + /admin/backup.zip
├── Authorization/CurrentUser.cs          — HttpContext-backed ICurrentUser implementation
├── HealthChecks/                         — DatabaseHealthCheck + DiskSpaceHealthCheck
├── Startup/
│   ├── BootstrapSeeder.cs                — seeds the admin user on first run from config
│   └── ForceChangePasswordMiddleware.cs  — pushes users into /account/change-password when MustChangePassword=true
├── Localization/                         — UiStrings (cs/en/de dictionaries) + Translator service
├── Components/
│   ├── AppShell.razor                    — <html> root, loads theme.js + dropzone.js
│   ├── Routes.razor
│   ├── Layout/ — MainLayout · NavMenu · EmptyLayout (login pages)
│   ├── Shared/ — MarkdownView (XSS-safe Markdig) · ThemeSwitcher · LanguageSwitcher
│   └── Pages/
│       ├── Login.razor · Login2fa.razor · ForgotPassword.razor · ResetPassword.razor
│       ├── ChangePassword.razor          — forced flow on first login
│       ├── Dashboard.razor               — stats + recent-activity feed
│       ├── Apps/AppsList.razor + AppDetail.razor
│       ├── Releases/ReleaseDetail.razor
│       ├── Account.razor                 — preferences, password, 2FA + backup codes, PATs, sessions
│       ├── Users.razor                   — admin-only user management
│       ├── Settings.razor                — admin-only CI token + SMTP + webhook secret + backup
│       ├── Security.razor                — IP block list
│       ├── Audit.razor                   — audit log with CSV export
│       ├── Analytics.razor               — download chart + platform/version breakdown
│       └── Storage.razor                 — per-app disk usage + cleanup
└── wwwroot/
    ├── app.css                           — single stylesheet, CSS variables for theming
    ├── theme.js                          — auto/light/dark via data-theme on <html>
    └── dropzone.js                       — drag-and-drop wiring for InputFile components
```

### `sdk/UpdateHub.Client/`

Drop-in NuGet library a consuming app embeds. Talks to the generic JSON
API at `/api/apps/{slug}/update`. Backward-compatible across the whole 1.x
line — see [docs/INTEGRATE.md](docs/INTEGRATE.md).

### `sdk/UpdateHub.Cli/`

Single-binary `updatehub` CLI. Reads `UPDATEHUB_URL` + `UPDATEHUB_TOKEN`
from env, posts artifacts via the CI endpoint. Lives in the same solution
so it always stays current with the server's expected form fields.

### `tools/GenerateHash/`

Tiny console app — `dotnet run -- yourpassword` prints the bcrypt hash to
paste into `UpdateHub__Admin__PasswordHash`.

## Request flow walkthroughs

### Public update check (generic JSON)

```
1. Client → GET /api/apps/my-app/update?version=1.0.0&platform=windows&arch=x64
2. PublicEndpoints → UpdateResolverService.CheckUpdateAsync
3. Resolver → IReleaseRepository.GetAllPublishedAsync (every Published in the channel)
4. Resolver → PickBestForVersion: sort by SemVer desc, skip releases the
   client doesn't satisfy MinFromVersion for, return highest remaining
5. Resolver picks the newest matching artifact (most recent upload for the
   given platform/arch) and builds the download URL
6. Public JSON returned to the client
```

### Artifact download

```
1. Client → GET /api/downloads/{artifactId}
2. ArtifactRepository.IncrementDownloadCountAsync  (legacy counter)
3. DownloadEventRepository.RecordAsync           (analytics row)
4. IArtifactStorage.OpenRead(...)                 → stream the bytes back
```

### CI upload

```
1. Pipeline → POST /api/ci/apps/{slug}/releases  with X-UpdateHub-Token / Bearer
2. CiEndpoints resolves the token:
     a) try as personal access token (per-user, audit-friendly)
     b) fall back to the per-app CI token, then the global CI token
3. AdminService.IngestCiUploadAsync — finds-or-creates a Draft release,
   stores the file (streamed straight to disk + hashed) and creates an
   Artifact row. Audit log records the upload with actor=`ci`.
4. Admin later flips the release to Published in the UI; that triggers
   the publish webhook (optionally per-app, HMAC-signed) and an SMTP
   notification, both off the request thread via the notification queue.
```

### Forgot-password

```
1. User → POST /account/forgot { who: "alice" or alice@... }
2. UserService.InitiatePasswordResetAsync — finds the user (silent if
   none / no email), invalidates older tokens, issues a hashed 30-min
   token, enqueues a notification email containing a link with the raw
   token.
3. User opens the link → POST /account/reset { token, new, confirm }
4. UserService.ConsumePasswordResetAsync — verifies the token, sets the
   new bcrypt hash, marks the token used, rotates SecurityStamp (any
   live cookie for the user is invalidated on next request by
   ForceChangePasswordMiddleware).
```

## Data model

```
App ──< Release ──< Artifact
     │
     └─< (optional WebhookUrl)

Release.MinFromVersion ── stepping-stone gate for direct upgrades

User ─< PersonalAccessToken
     ─< PasswordResetToken (transient, single use)
     │
     └── Email (optional, used for account / reset notifications)
                BackupCodes (10 single-use codes, hashed)
                TotpSecret  (encrypted via Data Protection)
                SecurityStamp (rotated on any password / role / disable change)

LoginAttempt   ── per-IP brute-force record
AuditEntry     ── append-only event log
DownloadEvent  ── analytics row, IP is SHA-256-hashed
AppSetting     ── key/value runtime config (CiToken, Smtp:*, Webhook:Secret)
```

## Authentication & authorization

| Path | How it authenticates |
|---|---|
| `/api/apps/*`, `/api/downloads/*` | None — public, rate-limited |
| `/api/ci/apps/*/releases` | `X-UpdateHub-Token` *or* `Authorization: Bearer` — PAT or shared secret |
| `/account/login` | Form POST, password verified against `User.PasswordHash`, optional TOTP |
| Everything else | Cookie issued at login (`UpdateHub.Web.Endpoints.AuthEndpoints`) |

Role enforcement happens on **two levels**:

1. UI: `<AuthorizeView Roles="Admin,Manager">` hides write actions
2. Service: `RoleGuard.Require(currentUser, …)` at the top of every
   write method on `AdminService` / `UserService` /
   `BruteForceProtectionService`. Even a hand-crafted request from a
   Viewer cannot mutate state.

`User.SecurityStamp` is rotated on password change, admin reset, role
change, deactivate, and "Sign out everywhere". `ForceChangePasswordMiddleware`
compares the cookie's stamp to the DB stamp on every request — mismatch =
sign out. That gives instant session revocation.

## Background work

`BackgroundNotificationQueue` (singleton + IHostedService) drains a
`Channel<Func<IServiceProvider, …, Task>>`. Each work item runs inside a
**freshly created DI scope**, so it can resolve scoped services
(DbContext, SettingsService) safely after the originating HTTP request
has ended. Publish webhooks, SMTP sends, IP-block notifications, and
user-event emails all flow through this queue.

## Migrations & state

SQLite + EF Core migrations under
`src/UpdateHub.Infrastructure/Migrations/`. `Database.Migrate()` is called
at startup from `Program.cs` (skipped in the `Testing` environment).
`BootstrapSeeder.SeedAdminAsync` runs right after migration: if the
`Users` table is empty, it creates the bootstrap admin from
`UpdateHub__Admin__*` config and carries over any legacy 2FA secret from
the old AppSetting-based storage.

To replace a layer:

- **Storage backend** — implement `IArtifactStorage`, register in
  `Infrastructure.DependencyInjection`. No other change.
- **DB engine** — change the `UseSqlite(...)` call to `UseNpgsql(...)`
  in `AddInfrastructure`, regenerate migrations.
- **Email transport** — implement `IEmailService` and register it
  ahead of `SmtpEmailService`.

## Testing

`tests/UpdateHub.Application.Tests/` covers business logic with
NSubstitute mocks — no DB needed. `tests/UpdateHub.Web.Tests/` uses
`WebApplicationFactory` with SQLite shared-cache in-memory to exercise
the HTTP surface end-to-end. Current coverage: 86 tests across both
projects.

## Further reading

- [INTEGRATE.md](docs/INTEGRATE.md) — embedding UpdateHub into a consuming app
- [ADMIN.md](docs/ADMIN.md) — deploying and operating the server
- [README.md](README.md) — high-level overview & quick start
