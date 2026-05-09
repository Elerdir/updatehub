# Architecture

UpdateHub follows **Clean Architecture** — dependencies point inward, inner layers know nothing about outer layers.

```
┌─────────────────────────────────────────────────────┐
│                   UpdateHub.Web                      │
│          (Blazor Server + Minimal API)               │
│                                                      │
│  ┌───────────────────────────────────────────────┐   │
│  │            UpdateHub.Infrastructure           │   │
│  │     (EF Core, SQLite, LocalArtifactStorage)   │   │
│  │                                               │   │
│  │  ┌─────────────────────────────────────────┐  │   │
│  │  │         UpdateHub.Application           │  │   │
│  │  │  (Services, Interfaces, Models/DTOs)    │  │   │
│  │  │                                         │  │   │
│  │  │  ┌───────────────────────────────────┐  │  │   │
│  │  │  │       UpdateHub.Domain            │  │  │   │
│  │  │  │  (Entities, Enums — no deps)      │  │  │   │
│  │  │  └───────────────────────────────────┘  │  │   │
│  │  └─────────────────────────────────────────┘  │   │
│  └───────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

## Projects & responsibilities

### UpdateHub.Domain
Pure C# — no NuGet dependencies.

```
Domain/
├── Entities/
│   ├── App.cs          — registered application
│   ├── Release.cs      — versioned release (Draft/Published/Archived)
│   └── Artifact.cs     — installer binary with SHA-256 + optional Tauri sig
└── Enums/
    ├── ReleaseChannel.cs   — Stable / Beta / Alpha
    └── ReleaseStatus.cs    — Draft / Published / Archived
```

### UpdateHub.Application
Business logic. Depends only on Domain.

```
Application/
├── Interfaces/
│   ├── IAppRepository.cs
│   ├── IReleaseRepository.cs
│   ├── IArtifactRepository.cs
│   └── IArtifactStorage.cs      — file storage abstraction
├── Services/
│   ├── AdminService.cs          — all admin CRUD operations
│   └── UpdateResolverService.cs — update check + Tauri manifest logic
└── Models/
    ├── UpdateCheckResult.cs
    └── TauriManifest.cs
```

Application services depend **only on interfaces** — they never know about EF Core or the filesystem. This makes them fully testable without a database.

### UpdateHub.Infrastructure
Implements Application interfaces using concrete technology.

```
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs                      — EF Core DbContext (SQLite)
│   └── Repositories/
│       ├── AppRepository.cs
│       ├── ReleaseRepository.cs
│       └── ArtifactRepository.cs
├── Storage/
│   └── LocalArtifactStorage.cs             — filesystem implementation of IArtifactStorage
└── DependencyInjection.cs                  — AddInfrastructure() extension method
```

### UpdateHub.Web
Presentation layer — registers DI, exposes HTTP endpoints, hosts Blazor UI.

```
Web/
├── Program.cs                              — bootstrap + auth endpoints
├── Endpoints/
│   ├── PublicEndpoints.cs                  — GET update, GET tauri, GET download
│   └── CiEndpoints.cs                      — POST upload from CI/CD
└── Components/
    ├── Pages/
    │   ├── Login.razor
    │   ├── Dashboard.razor
    │   ├── Apps/AppsList.razor + AppDetail.razor
    │   └── Releases/ReleaseDetail.razor
    └── Layout/
        ├── MainLayout.razor + NavMenu.razor
        └── EmptyLayout.razor               — used by Login (no sidebar)
```

Web pages inject **Application services only** (`AdminService`, `UpdateResolverService`). They are unaware of EF Core or the filesystem.

## Dependency graph

```
Web  →  Application  →  Domain
Web  →  Infrastructure  →  Application  →  Domain
```

Web references Infrastructure only in `Program.cs` to call `AddInfrastructure()`.

## Data model

```
App ──< Release ──< Artifact
```

| Entity | Key fields |
|---|---|
| App | Slug (unique), Name, Description |
| Release | Version, Channel, Status, IsMandatory, PublishedAt |
| Artifact | Platform, Architecture, FileName, StoredPath, Sha256, Signature |

## Update flow

```
1. App queries  GET /api/apps/{slug}/update?version=1.0.0&platform=windows&arch=x64
2. UpdateResolverService finds latest Published release for the channel
3. Compares versions (System.Version parsing, semver-like)
4. Returns has_update + download URL if a matching artifact exists
```

## Release workflow

```
CI upload → Draft ──[publish in admin UI]──→ Published ──[archive]──→ Archived
               ↑
         (add more artifacts per platform before publishing)
```

## Extending storage

To replace local filesystem with S3/MinIO, implement `IArtifactStorage` in Infrastructure and swap the registration in `DependencyInjection.cs`. No other code needs to change.
