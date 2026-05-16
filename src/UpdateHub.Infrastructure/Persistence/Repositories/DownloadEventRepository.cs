using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class DownloadEventRepository(AppDbContext db) : IDownloadEventRepository
{
    public async Task RecordAsync(DownloadEvent ev)
    {
        db.DownloadEvents.Add(ev);
        await db.SaveChangesAsync();
    }

    public async Task<List<(DateTime Day, int Count)>> CountByDayAsync(int days)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var rows = await db.DownloadEvents
            .Where(e => e.At >= since)
            .GroupBy(e => e.At.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToListAsync();
        return rows.Select(r => (r.Day, r.Count)).ToList();
    }

    public async Task<List<(string Platform, int Count)>> CountByPlatformAsync(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.DownloadEvents
            .Where(e => e.At >= since)
            .GroupBy(e => e.Platform)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        return rows.Select(r => (r.Key, r.Count)).ToList();
    }

    public async Task<List<(string AppSlug, string Version, int Count)>> TopVersionsAsync(int days, int take = 10)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.DownloadEvents
            .Where(e => e.At >= since)
            .GroupBy(e => new { e.AppSlug, e.Version })
            .Select(g => new { g.Key.AppSlug, g.Key.Version, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(take)
            .ToListAsync();
        return rows.Select(r => (r.AppSlug, r.Version, r.Count)).ToList();
    }

    public Task<List<DownloadEvent>> RecentAsync(int take) =>
        db.DownloadEvents.OrderByDescending(e => e.At).Take(take).ToListAsync();
}
