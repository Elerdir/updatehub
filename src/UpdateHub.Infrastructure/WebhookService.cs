using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using UpdateHub.Application.Interfaces;

namespace UpdateHub.Infrastructure;

public class WebhookService(IHttpClientFactory httpFactory, IConfiguration config) : IWebhookService
{
    public async Task NotifyPublishedAsync(string appSlug, string version, string? notes, string channel)
    {
        var url = config["UpdateHub:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(url)) return;

        var payload = new
        {
            app       = appSlug,
            version,
            channel,
            notes,
            timestamp = DateTime.UtcNow
        };

        try
        {
            using var client = httpFactory.CreateClient("webhook");
            client.Timeout = TimeSpan.FromSeconds(10);
            await client.PostAsJsonAsync(url, payload);
        }
        catch
        {
            // Webhook failure is non-fatal — never block the publish flow
        }
    }
}
