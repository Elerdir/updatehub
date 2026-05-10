namespace UpdateHub.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an email. Silently swallows exceptions — email failure must never block the caller.
    /// No-ops when SMTP is not configured.
    /// </summary>
    Task SendAsync(string subject, string body);
}
