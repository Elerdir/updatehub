using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;

namespace UpdateHub.Infrastructure;

public class WebhookService(
    IHttpClientFactory httpFactory,
    IConfiguration     config,
    SettingsService    settings)
    : IWebhookService
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

        // Serialize ourselves so we can sign the exact bytes that go on the wire.
        var json  = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            using var client = httpFactory.CreateClient("webhook");
            client.Timeout = TimeSpan.FromSeconds(10);

            using var msg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // HMAC-SHA256 of the payload, header lets the receiver verify origin.
            var secret = await settings.GetWebhookSecretAsync();
            if (!string.IsNullOrEmpty(secret))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var sig  = Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant();
                msg.Headers.Add("X-UpdateHub-Signature", $"sha256={sig}");
            }

            await client.SendAsync(msg);
        }
        catch
        {
            // Webhook failure is non-fatal — never block the publish flow.
        }
    }
}
