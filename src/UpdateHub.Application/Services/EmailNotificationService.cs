using UpdateHub.Application.Interfaces;

namespace UpdateHub.Application.Services;

/// <summary>
/// High-level notification helper — wraps IEmailService with domain-specific messages.
/// </summary>
public class EmailNotificationService(IEmailService email)
{
    public Task SendReleasePublishedAsync(string appSlug, string version, string channel) =>
        email.SendAsync(
            $"[UpdateHub] New release: {appSlug} v{version} ({channel})",
            $"""
            A new release has been published on UpdateHub.

            App:     {appSlug}
            Version: {version}
            Channel: {channel}
            Time:    {DateTime.UtcNow:u}
            """);

    public Task SendIpBlockedAsync(string ip, bool isManual) =>
        email.SendAsync(
            $"[UpdateHub] IP blocked: {ip}",
            $"""
            An IP address has been blocked on UpdateHub.

            IP:     {ip}
            Reason: {(isManual ? "Manually blocked by admin" : "Auto-blocked: too many failed login attempts")}
            Time:   {DateTime.UtcNow:u}
            """);

    public Task SendPasswordChangedAsync() =>
        email.SendAsync(
            "[UpdateHub] Admin password changed",
            $"""
            The UpdateHub admin password was changed.

            Time: {DateTime.UtcNow:u}

            If you did not do this, check your server immediately.
            """);
}
