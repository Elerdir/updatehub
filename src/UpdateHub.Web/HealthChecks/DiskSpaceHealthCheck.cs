using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UpdateHub.Web.HealthChecks;

public class DiskSpaceHealthCheck(string path, long minimumFreeBytes) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        try
        {
            // DriveInfo on Linux expects a mount point ("/"), not an arbitrary
            // path — so resolve the root of the filesystem the path lives on.
            var root   = Path.GetPathRoot(Path.GetFullPath(path));
            var drive  = new DriveInfo(string.IsNullOrEmpty(root) ? "/" : root);
            var freeGb = drive.AvailableFreeSpace / 1_073_741_824.0;
            var data   = new Dictionary<string, object> { ["free_gb"] = Math.Round(freeGb, 2) };

            return drive.AvailableFreeSpace < minimumFreeBytes
                ? Task.FromResult(HealthCheckResult.Degraded($"Low disk space: {freeGb:F1} GB free", data: data))
                : Task.FromResult(HealthCheckResult.Healthy($"{freeGb:F1} GB free", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Cannot read disk info", ex));
        }
    }
}
