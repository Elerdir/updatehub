using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using UpdateHub.Application.Interfaces;

namespace UpdateHub.Infrastructure.Email;

public class SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string subject, string body)
    {
        var host = config["UpdateHub:Smtp:Host"];
        var to   = config["UpdateHub:Smtp:To"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
            return; // SMTP not configured — silently skip

        var port     = int.TryParse(config["UpdateHub:Smtp:Port"], out var p) ? p : 587;
        var from     = config["UpdateHub:Smtp:From"] ?? "updatehub@localhost";
        var username = config["UpdateHub:Smtp:Username"];
        var password = config["UpdateHub:Smtp:Password"];

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
