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
            IArtifactRepository artifacts,
            IArtifactStorage storage) =>
        {
            var artifact = await artifacts.GetByIdAsync(artifactId);
            if (artifact is null) return Results.NotFound();

            await artifacts.IncrementDownloadCountAsync(artifactId);

            var stream = storage.OpenRead(artifact.StoredPath);
            return Results.File(stream, "application/octet-stream", artifact.FileName);
        }).RequireRateLimiting("public-api");
    }
}
