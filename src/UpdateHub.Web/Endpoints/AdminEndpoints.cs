using System.Globalization;
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
