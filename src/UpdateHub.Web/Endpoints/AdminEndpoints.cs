using System.Globalization;
using System.IO.Compression;
using System.Text;
using UpdateHub.Application.Services;

namespace UpdateHub.Web.Endpoints;

/// <summary>
/// Authenticated admin endpoints that don't fit on a Razor page — exports,
/// downloadables, etc. Mounted under /audit, /storage, etc.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapGet("/audit/export.csv", async (AuditService audit, HttpContext ctx) =>
        {
            var entries = await audit.GetRecentAsync(10_000);

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Actor,Action,EntityType,EntityId,Details,IpAddress");
            foreach (var e in entries)
            {
                sb.Append(e.OccurredAt.ToString("o", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(Csv(e.Actor)).Append(',');
                sb.Append(Csv(e.Action)).Append(',');
                sb.Append(Csv(e.EntityType ?? "")).Append(',');
                sb.Append(Csv(e.EntityId ?? "")).Append(',');
                sb.Append(Csv(e.Details ?? "")).Append(',');
                sb.AppendLine(Csv(e.IpAddress ?? ""));
            }

            ctx.Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"updatehub-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv\"";
            return Results.Text(sb.ToString(), "text/csv; charset=utf-8");
        }).RequireAuthorization();

        app.MapGet("/admin/backup.zip", (HttpContext ctx, IConfiguration cfg, AuditService audit) =>
        {
            // Snapshot of the metadata layer — the DB and the data-protection
            // key ring. Artifact files (artifacts/) are NOT included; they can
            // be huge and are easy to back up separately at the volume layer.
            if (!ctx.User.IsInRole("Admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var dbPath  = cfg["UpdateHub:DatabasePath"] ?? "updatehub.db";
            var dbAbs   = Path.GetFullPath(dbPath);
            var dbDir   = Path.GetDirectoryName(dbAbs) ?? ".";
            var keysDir = cfg["UpdateHub:DataProtectionKeysPath"]
                ?? Path.Combine(dbDir, "dp-keys");

            // Build the zip in-memory. The metadata footprint is small enough
            // (a few MB of DB + a handful of XML key files), so this avoids
            // littering the filesystem with temp files.
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    AddFile(zip, dbAbs,                   "updatehub.db");
                    AddFile(zip, dbAbs + "-shm",          "updatehub.db-shm"); // SQLite WAL companions
                    AddFile(zip, dbAbs + "-wal",          "updatehub.db-wal");
                    if (Directory.Exists(keysDir))
                    {
                        foreach (var f in Directory.EnumerateFiles(keysDir))
                            AddFile(zip, f, "dp-keys/" + Path.GetFileName(f));
                    }
                    var readme = zip.CreateEntry("README.txt");
                    using var w = new StreamWriter(readme.Open());
                    w.WriteLine($"UpdateHub backup taken {DateTime.UtcNow:u}");
                    w.WriteLine("Contains: SQLite database + Data Protection key ring.");
                    w.WriteLine("Restore: stop the container, unzip into /app/data, restart.");
                    w.WriteLine("Artifact files (artifacts/) are NOT included — back them up separately.");
                }
                bytes = ms.ToArray();
            }

            _ = audit.LogAsync("ExportBackup", actor: ctx.User.Identity?.Name ?? "?",
                entityType: "Backup", details: $"{bytes.Length} bytes");

            return Results.File(bytes, "application/zip", $"updatehub-backup-{stamp}.zip");
        }).RequireAuthorization();
    }

    private static void AddFile(ZipArchive zip, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath)) return;
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var src = File.OpenRead(sourcePath);
        using var dst = entry.Open();
        src.CopyTo(dst);
    }

    private static string Csv(string field)
    {
        // RFC 4180 quoting: wrap in quotes when the field contains a comma,
        // quote, or newline; escape internal quotes by doubling them.
        if (string.IsNullOrEmpty(field)) return "";
        var needsQuote = field.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuote) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
