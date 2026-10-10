namespace UpdateHub.Application.Models;

/// <summary>
/// SMTP credentials. Properties are nullable because every field is optional —
/// if Host or To is empty the email service silently skips sending.
/// </summary>
public record SmtpConfig(
    string? Host,
    int     Port,
    string? From,
    string? Username,
    string? Password,
    string? To)
{
    public static SmtpConfig Empty => new(null, 587, null, null, null, null);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(To);
}
