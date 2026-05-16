using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IDownloadEventRepository
{
    Task RecordAsync(DownloadEvent ev);

    /// <summary>Total downloads grouped by UTC day, last N days.</summary>
    Task<List<(DateTime Day, int Count)>> CountByDayAsync(int days);

    /// <summary>Downloads grouped by platform string (windows/macos/linux).</summary>
    Task<List<(string Platform, int Count)>> CountByPlatformAsync(int days);

    /// <summary>Most-downloaded versions in the last N days (top 10).</summary>
    Task<List<(string AppSlug, string Version, int Count)>> TopVersionsAsync(int days, int take = 10);

    /// <summary>Most recent N download events.</summary>
    Task<List<DownloadEvent>> RecentAsync(int take);
}
