using Microsoft.EntityFrameworkCore;
using UpdateHub.Web.Data;
using UpdateHub.Web.Data.Entities;
using UpdateHub.Web.Services;

namespace UpdateHub.Web.Endpoints;

public static class CiEndpoints
{
    public static void MapCiEndpoints(this WebApplication app)
    {
        // CI/CD upload endpoint — called from GitHub Actions
        app.MapPost("/api/ci/apps/{appSlug}/releases", async (
            string appSlug,
            HttpRequest request,
            AppDbContext db,
            ArtifactStorageService storage,
            IConfiguration config) =>
        {
            var expected = config["UpdateHub:CiToken"];
            if (string.IsNullOrEmpty(expected) || request.Headers["X-UpdateHub-Token"] != expected)
                return Results.Unauthorized();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data required" });

            var form     = await request.ReadFormAsync();
            var file     = form.Files["file"];
            var version  = form["version"].ToString();
            var platform = form["platform"].ToString();
            var arch     = form["arch"].FirstOrDefault() ?? "x64";
            var channel  = form["channel"].FirstOrDefault() ?? "stable";
            var notes    = form["release_notes"].ToString();
            var sig      = form["signature"].ToString();
            var mandatory = form["is_mandatory"] == "true";

            if (file is null || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(platform))
                return Results.BadRequest(new { error = "file, version and platform are required" });

            var appEntity = await db.Apps.FirstOrDefaultAsync(a => a.Slug == appSlug);
            if (appEntity is null)
                return Results.NotFound(new { error = $"App '{appSlug}' not registered" });

            var ch = channel.ToLower() switch
            {
                "beta"  => ReleaseChannel.Beta,
                "alpha" => ReleaseChannel.Alpha,
                _       => ReleaseChannel.Stable
            };

            // Reuse existing draft release for this version/channel, or create new one
            var release = await db.Releases.FirstOrDefaultAsync(r =>
                r.AppId == appEntity.Id && r.Version == version && r.Channel == ch);

            if (release is null)
            {
                release = new Release
                {
                    AppId = appEntity.Id,
                    Version = version.Trim(),
                    Channel = ch,
                    Status = ReleaseStatus.Draft,
                    ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    IsMandatory = mandatory
                };
                db.Releases.Add(release);
                await db.SaveChangesAsync();
            }

            using var stream = file.OpenReadStream();
            var (storedPath, sha256, fileSize) = await storage.StoreAsync(
                stream, file.FileName, appSlug, version);

            var artifact = new Artifact
            {
                ReleaseId     = release.Id,
                Platform      = platform.Trim().ToLower(),
                Architecture  = arch.Trim().ToLower(),
                FileName      = Path.GetFileName(file.FileName),
                StoredPath    = storedPath,
                Sha256        = sha256,
                Signature     = string.IsNullOrWhiteSpace(sig) ? null : sig.Trim(),
                FileSizeBytes = fileSize
            };
            db.Artifacts.Add(artifact);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                release_id  = release.Id,
                artifact_id = artifact.Id,
                sha256,
                message = $"Artifact uploaded. Release is in Draft — publish it via admin UI."
            });
        }).DisableAntiforgery();
    }
}
