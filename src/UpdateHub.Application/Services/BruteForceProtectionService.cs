using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Services;

public class BruteForceProtectionService(
    ILoginAttemptRepository repo,
    EmailNotificationService email,
    INotificationQueue notifications)
{
    public const int MaxFailedAttempts = 5;

    public async Task<bool> IsBlockedAsync(string ip)
    {
        var a = await repo.GetByIpAsync(ip);
        return a?.IsBlocked == true;
    }

    public async Task RecordFailureAsync(string ip, string? userAgent)
    {
        var a = await repo.GetByIpAsync(ip) ?? new LoginAttempt
        {
            IpAddress      = ip,
            FirstAttemptAt = DateTime.UtcNow
        };

        a.FailedAttempts++;
        a.LastAttemptAt = DateTime.UtcNow;
        a.LastUserAgent = userAgent;

        var justBlocked = false;
        if (!a.IsBlocked && a.FailedAttempts >= MaxFailedAttempts)
        {
            a.IsBlocked    = true;
            a.IsManualBlock = false;
            a.BlockedAt    = DateTime.UtcNow;
            justBlocked    = true;
        }

        await repo.UpsertAsync(a);

        if (justBlocked)
            notifications.Enqueue(_ => email.SendIpBlockedAsync(ip, isManual: false));
    }

    public async Task RecordSuccessAsync(string ip)
    {
        var a = await repo.GetByIpAsync(ip);
        if (a is null) return;

        a.FailedAttempts = 0;
        // manual block survives a successful login (admin chose to block)
        if (!a.IsManualBlock) a.IsBlocked = false;

        await repo.UpsertAsync(a);
    }

    public async Task UnblockAsync(string ip)
    {
        var a = await repo.GetByIpAsync(ip);
        if (a is null) return;
        a.IsBlocked    = false;
        a.IsManualBlock = false;
        a.FailedAttempts = 0;
        a.UnblockedAt  = DateTime.UtcNow;
        await repo.UpsertAsync(a);
    }

    public async Task BlockManuallyAsync(string ip)
    {
        var a = await repo.GetByIpAsync(ip) ?? new LoginAttempt
        {
            IpAddress      = ip,
            FirstAttemptAt = DateTime.UtcNow,
            LastAttemptAt  = DateTime.UtcNow
        };
        a.IsBlocked    = true;
        a.IsManualBlock = true;
        a.BlockedAt    = DateTime.UtcNow;
        await repo.UpsertAsync(a);
    }

    public async Task DeleteRecordAsync(string ip) => await repo.DeleteAsync(ip);

    public Task<List<LoginAttempt>> GetAllAsync() => repo.GetAllAsync();

    public Task<LoginAttempt?> GetByIpAsync(string ip) => repo.GetByIpAsync(ip);
}
