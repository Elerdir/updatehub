using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;

namespace UpdateHub.Web.Endpoints;

public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this WebApplication app)
    {
        app.MapGet("/api/apps/{appSlug}/tauri/latest.json", async (
            string appSlug,
            [FromQuery] string? channel,
            UpdateResolverService resolver) =>
        {
            var manifest = await resolver.GetTauriManifestAsync(appSlug, channel);
            if (manifest is null) return Results.NotFound();

            return Results.Ok(new
            {
                version   = manifest.Version,
                notes     = manifest.Notes,
                pub_date  = manifest.PubDate,
                platforms = manifest.Platforms.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new { signature = kvp.Value.Signature, url = kvp.Value.Url })
            });
        }).RequireRateLimiting("public-api");

        // ── electron-updater (YAML) ────────────────────────────────────────
        // Electron apps poll `<feedUrl>/latest.yml` (Windows), `latest-mac.yml`,
        // or `latest-linux.yml` and download from the URL inside.
        app.MapGet("/api/apps/{appSlug}/electron/{file}", async (
            string appSlug, string file,
            [FromQuery] string? channel,
            UpdateResolverService resolver) =>
        {
            // file = "latest.yml" | "latest-mac.yml" | "latest-linux.yml"
            var (platform, arch) = file switch
            {
                "latest.yml"       => ("windows", "x64"),
                "latest-mac.yml"   => ("macos",   "x64"),
                "latest-linux.yml" => ("linux",   "x64"),
                _ => ("", "")
            };
            if (platform == "") return Results.NotFound();

            var data = await resolver.GetLatestForFormatAsync(appSlug, platform, arch, channel);
            if (data is null) return Results.NotFound();
            var (rel, art, url) = data.Value;

            var yaml = string.Join("\n",
                $"version: {rel.Version}",
                $"files:",
                $"  - url: {url}",
                $"    sha512: ''",
                $"    size: {art.FileSizeBytes}",
                $"path: {Path.GetFileName(art.FileName)}",
                $"sha512: ''",
                $"releaseDate: '{(rel.PublishedAt ?? rel.CreatedAt):o}'",
                rel.ReleaseNotes is null ? "" : $"releaseNotes: |\n  {rel.ReleaseNotes.Replace("\n", "\n  ")}");
            return Results.Text(yaml, "application/x-yaml; charset=utf-8");
        }).RequireRateLimiting("public-api");

        // ── Sparkle (XML) ──────────────────────────────────────────────────
        // macOS / Windows apps using Sparkle / WinSparkle look for an
        // appcast.xml — an RSS-flavoured feed of releases.
        app.MapGet("/api/apps/{appSlug}/sparkle/appcast.xml", async (
            string appSlug,
            [FromQuery] string? channel,
            [FromQuery] string? platform,
            [FromQuery] string? arch,
            UpdateResolverService resolver) =>
        {
            var data = await resolver.GetLatestForFormatAsync(
                appSlug, platform ?? "macos", arch ?? "x64", channel);
            if (data is null) return Results.NotFound();
            var (rel, art, url) = data.Value;

            var xml =
$"""
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>{System.Net.WebUtility.HtmlEncode(appSlug)}</title>
    <item>
      <title>{System.Net.WebUtility.HtmlEncode($"Version {rel.Version}")}</title>
      <pubDate>{(rel.PublishedAt ?? rel.CreatedAt):R}</pubDate>
      <sparkle:version>{System.Net.WebUtility.HtmlEncode(rel.Version)}</sparkle:version>
      <description><![CDATA[{rel.ReleaseNotes ?? ""}]]></description>
      <enclosure url="{System.Net.WebUtility.HtmlEncode(url)}"
                 sparkle:version="{System.Net.WebUtility.HtmlEncode(rel.Version)}"
                 length="{art.FileSizeBytes}"
                 type="application/octet-stream" />
    </item>
  </channel>
</rss>
""";
            return Results.Text(xml, "application/xml; charset=utf-8");
        }).RequireRateLimiting("public-api");

        // ── Velopack (JSON) ────────────────────────────────────────────────
        // Successor to Squirrel.Windows — looks for releases.json containing
        // an array of available versions with their direct download URLs.
        app.MapGet("/api/apps/{appSlug}/velopack/releases.json", async (
            string appSlug,
            [FromQuery] string? channel,
            [FromQuery] string? platform,
            [FromQuery] string? arch,
            UpdateResolverService resolver) =>
        {
            var data = await resolver.GetLatestForFormatAsync(
                appSlug, platform ?? "windows", arch ?? "x64", channel);
            if (data is null) return Results.NotFound();
            var (rel, art, url) = data.Value;
            return Results.Ok(new
            {
                releases = new[] {
                    new {
                        version    = rel.Version,
                        url,
                        size       = art.FileSizeBytes,
                        sha256     = art.Sha256,
                        type       = "full",
                        notes      = rel.ReleaseNotes,
                        pubDate    = (rel.PublishedAt ?? rel.CreatedAt).ToString("o"),
                        mandatory  = rel.IsMandatory,
                    }
                }
            });
        }).RequireRateLimiting("public-api");

        app.MapGet("/api/apps/{appSlug}/update", async (
            string appSlug,
            [FromQuery] string? version,
            [FromQuery] string? platform,
            [FromQuery] string? arch,
            [FromQuery] string? channel,
            UpdateResolverService resolver) =>
        {
            if (string.IsNullOrWhiteSpace(version))
                return Results.BadRequest(new { error = "version parameter is required" });

            var result = await resolver.CheckUpdateAsync(appSlug, version, platform, arch, channel);
            if (result is null) return Results.NotFound();

            return Results.Ok(new
            {
                has_update    = result.HasUpdate,
                version       = result.Version,
                release_notes = result.ReleaseNotes,
                download_url  = result.DownloadUrl,
                sha256        = result.Sha256,
                is_mandatory  = result.IsMandatory,
                channel       = result.Channel
            });
        }).RequireRateLimiting("public-api");

        app.MapGet("/api/downloads/{artifactId:guid}", async (
            Guid artifactId,
            HttpContext ctx,
            IArtifactRepository artifacts,
            IArtifactStorage storage,
            IDownloadEventRepository downloads,
            IReleaseRepository releases) =>
        {
            var artifact = await artifacts.GetByIdAsync(artifactId);
            if (artifact is null) return Results.NotFound();

            await artifacts.IncrementDownloadCountAsync(artifactId);

            // Best-effort analytics event — failure must not break the download.
            try
            {
                var release = await releases.GetByIdAsync(artifact.ReleaseId);
                var ip      = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
                var ipHash  = string.IsNullOrEmpty(ip) ? null :
                    Convert.ToHexString(System.Security.Cryptography.SHA256
                        .HashData(System.Text.Encoding.UTF8.GetBytes(ip)))[..16].ToLowerInvariant();
                await downloads.RecordAsync(new UpdateHub.Domain.Entities.DownloadEvent
                {
                    ArtifactId   = artifact.Id,
                    AppId        = release?.AppId ?? Guid.Empty,
                    AppSlug      = release?.App.Slug ?? "",
                    Version      = release?.Version ?? "",
                    Platform     = artifact.Platform,
                    Architecture = artifact.Architecture,
                    UserAgent    = ctx.Request.Headers.UserAgent.ToString(),
                    IpHash       = ipHash,
                });
            }
            catch { /* analytics is non-essential */ }

            var stream = storage.OpenRead(artifact.StoredPath);
            return Results.File(stream, "application/octet-stream", artifact.FileName);
        }).RequireRateLimiting("public-api");
    }
}
