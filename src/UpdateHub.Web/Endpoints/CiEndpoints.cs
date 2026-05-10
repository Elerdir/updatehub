using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Web.Endpoints;

public static class CiEndpoints
{
    public static void MapCiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ci/apps/{appSlug}/releases", async (
            string appSlug,
            HttpRequest request,
            IAppRepository appRepo,
            IReleaseRepository releaseRepo,
            IArtifactRepository artifactRepo,
            IArtifactStorage storage,
            SettingsService settings,
            IConfiguration config) =>
        {
            // Per-app token takes priority; fall back to global token
            var appForToken = await appRepo.GetBySlugAsync(appSlug);
            var appToken    = appForToken?.CiToken;

            var dbToken     = await settings.GetCiTokenAsync();
            var globalToken = string.IsNullOrWhiteSpace(dbToken) ? config["UpdateHub:CiToken"] : dbToken;

            var expected    = string.IsNullOrWhiteSpace(appToken) ? globalToken : appToken;

            if (string.IsNullOrEmpty(expected) || request.Headers["X-UpdateHub-Token"] != expected)
                return Results.Unauthorized();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data required" });

            var form      = await request.ReadFormAsync();
            var file      = form.Files["file"];
            var version   = form["version"].ToString();
            var platform  = form["platform"].ToString();
            var arch      = form["arch"].FirstOrDefault() ?? "x64";
            var channel   = form["channel"].FirstOrDefault() ?? "stable";
            var notes     = form["release_notes"].ToString();
            var sig       = form["signature"].ToString();
            var mandatory = form["is_mandatory"] == "true";

            if (file is null || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(platform))
                return Results.BadRequest(new { error = "file, version and platform are required" });

            var appEntity = await appRepo.GetBySlugAsync(appSlug);
            if (appEntity is null)
                return Results.NotFound(new { error = $"App '{appSlug}' not registered in UpdateHub" });

            var ch = channel.ToLower() switch
            {
                "beta"  => ReleaseChannel.Beta,
                "alpha" => ReleaseChannel.Alpha,
                _       => ReleaseChannel.Stable
            };

            var release = appEntity.Releases.FirstOrDefault(r =>
                r.Version == version.Trim() && r.Channel == ch);

            if (release is null)
            {
                release = await releaseRepo.CreateAsync(new Release
                {
                    AppId        = appEntity.Id,
                    Version      = version.Trim(),
                    Channel      = ch,
                    Status       = ReleaseStatus.Draft,
                    ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    IsMandatory  = mandatory
                });
            }

            using var stream = file.OpenReadStream();
            var (storedPath, sha256, fileSize) = await storage.StoreAsync(
                stream, file.FileName, appSlug, version);

            var artifact = await artifactRepo.CreateAsync(new Artifact
            {
                ReleaseId     = release.Id,
                Platform      = platform.Trim().ToLower(),
                Architecture  = arch.Trim().ToLower(),
                FileName      = Path.GetFileName(file.FileName),
                StoredPath    = storedPath,
                Sha256        = sha256,
                Signature     = string.IsNullOrWhiteSpace(sig) ? null : sig.Trim(),
                FileSizeBytes = fileSize
            });

            return Results.Ok(new
            {
                release_id  = release.Id,
                artifact_id = artifact.Id,
                sha256,
                message = "Artifact uploaded. Release is in Draft — publish it via admin UI."
            });
        }).DisableAntiforgery();
    }
}
