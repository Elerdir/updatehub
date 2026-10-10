using UpdateHub.Application.Interfaces;

namespace UpdateHub.Application.Services;

/// <summary>
/// High-level notification helper — wraps IEmailService with domain-specific messages.
/// </summary>
public class EmailNotificationService(IEmailService email, BaseUrlAccessor? baseUrl = null)
{
    private string? baseUrlForLinks => baseUrl?.BaseUrl;

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

    public Task SendPasswordChangedAsync(string? username = null) =>
        email.SendAsync(
            "[UpdateHub] Password changed",
            $"""
            A user password was changed on UpdateHub.

            User: {username ?? "(unknown)"}
            Time: {DateTime.UtcNow:u}

            If you did not do this, check your server immediately.
            """);

    public Task SendUserCreatedAsync(string username, string role, string createdByName) =>
        email.SendAsync(
            $"[UpdateHub] New user created: {username}",
            $"""
            A new user account was created on UpdateHub.

            Username: {username}
            Role:     {role}
            Created by: {createdByName}
            Time:     {DateTime.UtcNow:u}

            The user must change their temporary password on first login.
            """);

    public Task SendPasswordResetAsync(string username, string resetByName) =>
        email.SendAsync(
            $"[UpdateHub] Password reset: {username}",
            $"""
            A user password was reset on UpdateHub by an administrator.

            User:       {username}
            Reset by:   {resetByName}
            Time:       {DateTime.UtcNow:u}

            The user must change the temporary password on next login.
            """);

    public Task SendTestAsync() =>
        email.SendAsync(
            "[UpdateHub] Test message",
            $"This is a test message from UpdateHub. SMTP is configured correctly. Time: {DateTime.UtcNow:u}");

    // ── User-targeted notifications (go to the user's own email) ─────────────
    // Passwords are NEVER included; the admin delivers them out-of-band.

    public Task SendAccountCreatedToUserAsync(string? userEmail, string username, string role) =>
        string.IsNullOrWhiteSpace(userEmail)
            ? Task.CompletedTask
            : email.SendAsync(
                "[UpdateHub] Your account has been created",
                $"""
                Hello {username},

                An UpdateHub account has been created for you.

                Username: {username}
                Role:     {role}
                Time:     {DateTime.UtcNow:u}

                Your temporary password will be delivered to you separately
                by the administrator. You will be required to change it on
                your first sign-in.

                If you did not expect this, contact the administrator.
                """,
                toOverride: userEmail);

    public Task SendPasswordResetLinkAsync(string? userEmail, string username, string token)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return Task.CompletedTask;
        var link = $"{baseUrlForLinks ?? "(server URL)"}/account/reset?token={token}";
        return email.SendAsync(
            "[UpdateHub] Password reset request",
            $"""
            Hello {username},

            We received a request to reset your UpdateHub password.

            If this was you, open the link below within 30 minutes and
            choose a new password:

            {link}

            If you did NOT request this, you can safely ignore this email —
            no change has been made to your account.

            Time: {DateTime.UtcNow:u}
            """,
            toOverride: userEmail);
    }

    public Task SendPasswordResetToUserAsync(string? userEmail, string username) =>
        string.IsNullOrWhiteSpace(userEmail)
            ? Task.CompletedTask
            : email.SendAsync(
                "[UpdateHub] Your password has been reset",
                $"""
                Hello {username},

                An administrator has reset your UpdateHub password.

                Time: {DateTime.UtcNow:u}

                Your new temporary password will be delivered to you separately
                by the administrator. You will be required to change it on
                your next sign-in.

                If you did not request this, contact the administrator
                immediately — your account may be compromised.
                """,
                toOverride: userEmail);
}
