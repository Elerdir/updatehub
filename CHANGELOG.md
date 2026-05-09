# Changelog

All notable changes to UpdateHub will be documented here.

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
