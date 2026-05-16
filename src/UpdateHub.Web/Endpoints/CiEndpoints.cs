using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
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
            SettingsService settings,
            UserService userSvc,
            AdminService admin,
            IConfiguration config) =>
        {
            // Single lookup — used for both token resolution and the upload itself
            var appEntity = await appRepo.GetBySlugAsync(appSlug);

            var presented = request.Headers["X-UpdateHub-Token"].ToString();

            // Bearer header carries a personal access token (user-scoped)
            var bearer = request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(presented) && bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                presented = bearer["Bearer ".Length..].Trim();

            var authorized = false;

            // Try matching against the personal access tokens table first
            if (!string.IsNullOrEmpty(presented))
            {
                var owner = await userSvc.VerifyPersonalAccessTokenAsync(presented);
                if (owner is not null && (owner.Role == UpdateHub.Domain.Enums.UserRole.Admin
                                       || owner.Role == UpdateHub.Domain.Enums.UserRole.Manager))
                    authorized = true;
            }

            if (!authorized)
            {
                // Fall back to per-app / global CI token (legacy shared-secret)
                var appToken    = appEntity?.CiToken;
                var dbToken     = await settings.GetCiTokenAsync();
                var globalToken = string.IsNullOrWhiteSpace(dbToken) ? config["UpdateHub:CiToken"] : dbToken;
                var expected    = string.IsNullOrWhiteSpace(appToken) ? globalToken : appToken;

                if (!string.IsNullOrEmpty(expected) && presented == expected)
                    authorized = true;
            }

            if (!authorized) return Results.Unauthorized();

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
            var minFrom   = form["min_from_version"].ToString();

            if (file is null || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(platform))
                return Results.BadRequest(new { error = "file, version and platform are required" });

            if (appEntity is null)
                return Results.NotFound(new { error = $"App '{appSlug}' not registered in UpdateHub" });

            var ch = channel.ToLower() switch
            {
                "beta"  => ReleaseChannel.Beta,
                "alpha" => ReleaseChannel.Alpha,
                _       => ReleaseChannel.Stable
            };

            await using var stream = file.OpenReadStream();
            var (release, artifact) = await admin.IngestCiUploadAsync(
                appEntity, version, ch, notes, mandatory,
                stream, file.FileName, platform, arch, sig, minFrom);

            return Results.Ok(new
            {
                release_id  = release.Id,
                artifact_id = artifact.Id,
                sha256      = artifact.Sha256,
                message = "Artifact uploaded. Release is in Draft — publish it via admin UI."
            });
        }).DisableAntiforgery();
    }
}
