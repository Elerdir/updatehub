using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;

namespace UpdateHub.Infrastructure.Email;

public class SmtpEmailService(
    SettingsService settings,
    IConfiguration  config,
    ILogger<SmtpEmailService> logger)
    : IEmailService
{
    public async Task SendAsync(string subject, string body, string? toOverride = null)
    {
        var cfg = await settings.GetSmtpConfigAsync();

        // Effective config = DB value if set, otherwise the environment / appsettings value.
        var host     = cfg.Host     ?? config["UpdateHub:Smtp:Host"];
        var defaultTo = cfg.To      ?? config["UpdateHub:Smtp:To"];
        var to       = !string.IsNullOrWhiteSpace(toOverride) ? toOverride : defaultTo;
        var port     = cfg.Host     is null
            ? (int.TryParse(config["UpdateHub:Smtp:Port"], out var p) ? p : 587)
            : cfg.Port;
        var from     = cfg.From     ?? config["UpdateHub:Smtp:From"]     ?? "updatehub@localhost";
        var username = cfg.Username ?? config["UpdateHub:Smtp:Username"];
        var password = cfg.Password ?? config["UpdateHub:Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
            return; // SMTP not configured (or no recipient) — silently skip

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body    = new TextPart("plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable);
            if (!string.IsNullOrWhiteSpace(username))
                await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }
        catch (Exception ex)
        {
            // Email failure is non-fatal — log and continue
            logger.LogWarning(ex, "Failed to send email: {Subject}", subject);
        }
    }
}
