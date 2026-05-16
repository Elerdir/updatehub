# Integration guide

How to wire UpdateHub into an application that wants to check for and
download updates. Pick the section that matches your stack.

## 0. Conceptual model — what UpdateHub gives you

Every app you publish through UpdateHub gets:

| Endpoint | Purpose | Format |
|---|---|---|
| `GET /api/apps/{slug}/update?version=…&platform=…&arch=…` | Generic update check | JSON |
| `GET /api/apps/{slug}/tauri/latest.json` | Tauri updater feed | Tauri JSON |
| `GET /api/apps/{slug}/electron/latest.yml` (and `latest-mac.yml`, `latest-linux.yml`) | electron-updater feed | YAML |
| `GET /api/apps/{slug}/sparkle/appcast.xml` | Sparkle / WinSparkle feed | XML (RSS) |
| `GET /api/apps/{slug}/velopack/releases.json` | Velopack / next-gen Squirrel feed | JSON |
| `GET /api/downloads/{artifactId}` | Stream the installer bytes | binary |

Pick whichever your framework already speaks; UpdateHub serves the same
underlying release in any of those wrappings.

`{slug}` is the unique identifier you give the app inside UpdateHub
(e.g. `container-commander`). It is part of every URL.

### Channels

Each release sits in a channel: **stable**, **beta**, or **alpha**.
Append `?channel=beta` to any of the update endpoints to subscribe a
specific build of your app to that channel.

### Stepping-stone upgrades

A release may declare `MinFromVersion` (e.g. `5.0.0`). Clients whose
current version is lower are silently rerouted to the highest release
they *can* install directly. On the next check they get the next step,
and so on. The client code stays exactly the same — your updater
already polls after a successful install.

## 1. .NET / Avalonia — using the UpdateHub.Client SDK

### Install

The SDK lives at `sdk/UpdateHub.Client/`. Either reference it directly
or pack it as NuGet:

```bash
dotnet pack sdk/UpdateHub.Client/UpdateHub.Client.csproj -c Release
# → bin/Release/UpdateHub.Client.1.0.0.nupkg
```

In your app:

```xml
<PackageReference Include="UpdateHub.Client" Version="1.*" />
```

### Use

```csharp
using UpdateHub.Client;

var client  = new UpdateHubClient("https://updates.example.com", appSlug: "my-app");
var current = "1.0.0";          // your assembly version

var result = await client.CheckForUpdateAsync(current);   // auto-detects OS + arch
if (!result.HasUpdate) return;

Console.WriteLine($"New: {result.LatestVersion}  ({result.ReleaseNotes})");

var tempPath = Path.Combine(Path.GetTempPath(), "MyApp-update.exe");
await client.DownloadAsync(result.DownloadUrl!, tempPath,
    progress: new Progress<double>(p => Console.WriteLine($"{p:P0}")));

if (!UpdateHubClient.VerifySha256(tempPath, result.Sha256!))
    throw new InvalidOperationException("Hash mismatch — abort install.");

// Hand the installer to the OS and exit so it can replace your binary
Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
Environment.Exit(0);
```

The SDK constructor that takes a pre-built `HttpClient` is provided for
DI scenarios:

```csharp
services.AddHttpClient<UpdateHubClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new UpdateHubClient(http, cfg["UpdateServer"]!, cfg["AppSlug"]!);
});
```

### Backward compatibility

The SDK contract has not changed across the 1.x line. **If your app
already uses an older `UpdateHub.Client` build, you do not need to swap
it out**. The server-side stepping-stone logic is transparent: the SDK
keeps doing `Check → Download → Install → Restart → Check again` and
the server hands it the right release at each step.

Rebuild against the latest source only when you want one of the new
optional SDK features (none exist today — the file is stable).

## 2. Tauri

`tauri.conf.json`:

```json
{
  "updater": {
    "active": true,
    "endpoints": [
      "https://updates.example.com/api/apps/my-app/tauri/latest.json?channel=stable"
    ],
    "dialog": true,
    "pubkey": "BASE64_OF_YOUR_TAURI_PUBLIC_KEY"
  }
}
```

When you upload artifacts to UpdateHub from your build pipeline, set the
`signature` form field to the contents of the matching `.sig` file that
Tauri produces. UpdateHub serves it back inside the manifest exactly as
Tauri expects.

Tauri's updater does **not** send a `version` query parameter, so the
stepping-stone gating does not apply — Tauri clients always get the
absolute latest signed release. If you need to enforce sequential
upgrades for a Tauri app, use the generic JSON API and write a small
custom updater in your app.

## 3. Electron / electron-updater

`package.json`:

```json
{
  "build": {
    "publish": [{
      "provider": "generic",
      "url": "https://updates.example.com/api/apps/my-app/electron"
    }]
  }
}
```

electron-updater will fetch `latest.yml` (Windows), `latest-mac.yml`, or
`latest-linux.yml` from that path, depending on the OS it's running on.
UpdateHub serves the YAML form derived from your most recently
published artifact for that platform.

## 4. Sparkle (macOS) / WinSparkle (Windows)

```c
win_sparkle_set_appcast_url(
  "https://updates.example.com/api/apps/my-app/sparkle/appcast.xml?platform=windows&arch=x64");
win_sparkle_init();
```

UpdateHub renders an RSS appcast pointing to your installer. Pass
`?platform=` and `?arch=` to match the binary your installer expects.

## 5. Velopack / Clowd.Squirrel

```csharp
var manager = new UpdateManager(new GithubSource(
    "https://updates.example.com/api/apps/my-app/velopack/releases.json"));
```

(Substitute the correct source type for your framework — any
`UpdateSource` accepting a `releases.json` URL works.)

## 6. Anything else — call the generic JSON yourself

```http
GET https://updates.example.com/api/apps/my-app/update?version=1.0.0&platform=linux&arch=x64
```

```json
{
  "has_update": true,
  "version": "1.5.0",
  "release_notes": "## What's new\n- …",
  "download_url": "https://updates.example.com/api/downloads/2c1d…",
  "sha256": "1a2b3c…",
  "is_mandatory": false,
  "channel": "stable"
}
```

Implementation in any language is a `GET` and a JSON parse.
Verify `sha256` after downloading. Restart the app and check again to
follow stepping-stone chains.

## 7. Uploading releases from your build pipeline

You have three options. Pick whichever your CI/CD pipeline finds most
natural.

### 7.1 The `updatehub` CLI

```bash
# In your CI job, after producing the installer:
export UPDATEHUB_URL=https://updates.example.com
export UPDATEHUB_TOKEN=$UPDATEHUB_CI_TOKEN     # or your personal access token

updatehub upload my-app 1.5.0 ./dist/MyApp-Setup.exe \
  --platform windows --arch x64 --channel stable \
  --notes "$(cat CHANGELOG-1.5.0.md)" \
  --signature "$(cat ./dist/MyApp-Setup.exe.sig)"   # only for Tauri
```

The CLI lives at `sdk/UpdateHub.Cli/`. Build a single-file binary with
`dotnet publish -c Release -r <rid> --self-contained` and check it in
or distribute it through your package manager of choice.

### 7.2 GitHub Actions

A complete example workflow lives at
[`docs/upload-release.example.yml`](upload-release.example.yml). Copy it
into your app repo under `.github/workflows/`.

Required secrets in the app repo:
- `UPDATEHUB_URL` — your server URL
- `UPDATEHUB_CI_TOKEN` — a CI token (per-app or global) or a personal access token

Required repo variable:
- `APP_SLUG`

### 7.3 Raw curl

```bash
curl -f -X POST "https://updates.example.com/api/ci/apps/my-app/releases" \
  -H "X-UpdateHub-Token: $UPDATEHUB_CI_TOKEN" \
  -F "file=@./dist/MyApp-Setup.exe" \
  -F "version=1.5.0" \
  -F "platform=windows" \
  -F "arch=x64" \
  -F "channel=stable" \
  -F "release_notes=Bug fixes and improvements" \
  -F "is_mandatory=false" \
  -F "min_from_version=1.4.0"        # optional — see "Stepping-stone upgrades"
```

The endpoint accepts the token under either `X-UpdateHub-Token` or
`Authorization: Bearer …`. A token can be:

- The **global CI token** from the Settings page
- A **per-app CI token** (override on the App detail page) — useful so
  each app's pipeline has its own secret
- A **personal access token** generated under My Account (best for
  auditability; each upload is attributed to the user that owns the token)

Uploaded releases land as **Draft**. Open the admin UI, review, then
click Publish — that's when notifications fire and the release becomes
visible to clients.

### 7.4 Multiple artifacts per release

A single release can have many artifacts. Common patterns:

- Same platform, different formats (`Setup.exe` + `portable.zip` for
  Windows x64) — the update endpoint returns the **most recently
  uploaded** file matching the requested platform/arch, so upload the
  one the updater should use **last**.
- Same platform, different architectures (Windows x64 + Windows arm64)
  — clients pass `?arch=` and get the right binary.
- Different platforms — Windows + macOS + Linux side by side under
  the same version.

Make a second `POST` to the same `/api/ci/apps/{slug}/releases` with the
same `version` and the additional file. UpdateHub recognises the
existing draft and adds an artifact rather than creating a duplicate
release.

## 8. End-to-end checklist for a new application

1. **In the admin UI**: register the app (Applications → New Application).
   Pick a slug — that's the value of `{slug}` in every URL.
2. **Get a CI token**: either reveal the global token on Settings, rotate
   a per-app token on the app detail page, or create a personal access
   token on the My Account page.
3. **Wire your build pipeline**: pipe the produced installer through
   `updatehub upload`, the example GitHub Actions workflow, or raw curl.
4. **Wire your app's updater**: pick the SDK / framework section above
   and point it at `https://YOUR_SERVER/api/apps/YOUR_SLUG/…`.
5. **Publish a release**: upload an artifact, then click Publish in the
   admin UI. The first time you do this, also subscribe an email under
   `Settings → Email (SMTP)` so you get a notification next time.
6. **Test the round-trip**: install version N-1 of your app on a
   machine, run it, watch it discover and download version N.

## 9. Migrating an app that already uses UpdateHub

You don't need to do anything specific. The 1.x server API is
backward-compatible and the `UpdateHub.Client` SDK has not changed its
public surface across feature additions. Rebuild only if you want to:

- Adopt the new `updatehub` CLI in your pipeline instead of curl
- Switch from the shared CI token to a personal access token
- Adopt one of the manifest formats (electron-updater, Sparkle, Velopack)
  in place of the generic JSON

Otherwise: nothing. Keep shipping.
