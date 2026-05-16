namespace UpdateHub.Application.Interfaces;

public interface IWebhookService
{
    /// <summary>
    /// Sends a POST notification when a release is published.
    /// Silently swallows exceptions — webhook failure must never block the publish flow.
    /// </summary>
    Task NotifyPublishedAsync(string appSlug, string version, string? notes, string channel);
}
