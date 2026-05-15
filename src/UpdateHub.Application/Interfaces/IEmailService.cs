namespace UpdateHub.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an email. Silently swallows exceptions — email failure must never block the caller.
    /// No-ops when SMTP is not configured.
    /// </summary>
    /// <param name="toOverride">
    /// When set, the message goes to this address instead of the global <c>Smtp:To</c>
    /// (admin notification mailbox). Used for user-targeted notifications
    /// (account created, password reset).
    /// </param>
    Task SendAsync(string subject, string body, string? toOverride = null);
}
