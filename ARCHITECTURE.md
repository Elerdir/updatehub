# Architecture

## Overview

UpdateHub is a single ASP.NET Core 10 application with Blazor Server for the admin UI and Minimal API for the public/CI endpoints. It uses SQLite for the database and local filesystem for artifact storage.

```
┌─────────────────────────────────────────┐
│              UpdateHub.Web              │
│                                         │
│  ┌─────────────┐   ┌─────────────────┐  │
│  │  Blazor UI  │   │   Minimal API   │  │
│  │  (admin)    │   │  (public + CI)  │  │
│  └──────┬──────┘   └────────┬────────┘  │
│         │                   │           │
│  ┌──────▼───────────────────▼────────┐  │
│  │           Services                │  │
│  │  AdminService / UpdateResolver /  │  │
│  │  ArtifactStorageService           │  │
│  └──────────────────┬────────────────┘  │
│                     │                   │
│  ┌──────────────────▼────────────────┐  │
│  │  AppDbContext (EF Core + SQLite)   │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
         │                    │
    updatehub.db         artifacts/
    (SQLite)             (files on disk)
```

## Project Structure

```
src/UpdateHub.Web/
├── Program.cs                    — App bootstrap, DI, auth, endpoint registration
├── Data/
│   ├── AppDbContext.cs           — EF Core DbContext
│   └── Entities/
│       ├── App.cs                — Registered application
│       ├── Release.cs            — Version release
│       ├── Artifact.cs           — Installer/binary file
│       └── Enums.cs              — ReleaseChannel, ReleaseStatus
├── Services/
│   ├── AdminService.cs           — CRUD operations for admin UI
│   ├── UpdateResolverService.cs  — Update check logic (Tauri + generic)
│   └── ArtifactStorageService.cs — File storage + SHA-256 hashing
├── Endpoints/
│   ├── PublicEndpoints.cs        — GET update, GET tauri manifest, GET download
│   └── CiEndpoints.cs            — POST upload from CI/CD
└── Components/
    ├── App.razor / Routes.razor  — Blazor root
    ├── Layout/
    │   ├── MainLayout.razor      — Sidebar + content wrapper
    │   ├── NavMenu.razor         — Navigation
    │   └── EmptyLayout.razor     — Used for Login page
    └── Pages/
        ├── Login.razor
        ├── Dashboard.razor
        ├── Apps/
        │   ├── AppsList.razor    — List + create apps
        │   └── AppDetail.razor   — App info + release list
        └── Releases/
            └── ReleaseDetail.razor — Artifacts, publish/archive
```

## Data Model

```
App
├── Id (Guid PK)
├── Slug (string, unique)       — used in API URLs
├── Name (string)
├── Description (string?)
├── CreatedAt (DateTime)
└── Releases []

Release
├── Id (Guid PK)
├── AppId (Guid FK → App)
├── Version (string)            — semver, e.g. "1.2.3"
├── Channel (enum)              — Stable / Beta / Alpha
├── Status (enum)               — Draft / Published / Archived
├── ReleaseNotes (string?)
├── IsMandatory (bool)
├── PublishedAt (DateTime?)
├── CreatedAt (DateTime)
└── Artifacts []

Artifact
├── Id (Guid PK)
├── ReleaseId (Guid FK → Release)
├── Platform (string)           — windows / macos / linux
├── Architecture (string)       — x64 / arm64 / x86
├── FileName (string)
├── StoredPath (string)         — absolute path on disk
├── Sha256 (string)             — hex string
├── Signature (string?)         — Tauri Ed25519 signature
├── FileSizeBytes (long)
└── CreatedAt (DateTime)
```

## Authentication

Admin UI uses cookie-based authentication. Credentials are stored in configuration (not the DB) — a single admin user with a bcrypt-hashed password.

Public API endpoints have no authentication.

CI/CD endpoint uses a static bearer token (`X-UpdateHub-Token` header).

## Update Resolution Logic

`UpdateResolverService.CheckUpdateAsync`:
1. Find the latest **Published** release for the given app + channel
2. Compare versions using `System.Version` (semver-like parsing)
3. Find a matching artifact for the requested platform + architecture
4. Return the result with a download URL

`UpdateResolverService.GetTauriManifestAsync`:
1. Same as above, but formats the response in Tauri's expected JSON shape
2. Maps platform/arch to Tauri platform keys (e.g. `windows-x86_64`)
3. Only includes artifacts that have a Tauri signature

## Release Workflow

```
CI upload  →  Draft  →  [admin reviews]  →  Published  →  Archived
                              ↑
                    Upload more artifacts
                    (e.g. macOS, Linux builds)
```

A Draft release can accumulate multiple artifacts (one per platform/arch) before being published. Once published, clients will start receiving it.

## Storage

Artifacts are stored at:
```
{StoragePath}/{appSlug}/{version}/{filename}
```

SHA-256 is computed during upload and stored in the database. The download endpoint streams the file directly.

For production, the storage path should be a persistent volume (see Dockerfile).

## Scalability Notes

This is intentionally a simple personal-use tool. For the intended use case (handful of apps, small user base), SQLite + local filesystem is perfectly adequate.

If you ever need to scale:
- Replace SQLite with PostgreSQL (change the EF Core provider + connection string)
- Replace local storage with S3/MinIO (implement an `IArtifactStorage` interface)
