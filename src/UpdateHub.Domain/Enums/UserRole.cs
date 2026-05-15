namespace UpdateHub.Domain.Enums;

public enum UserRole
{
    /// <summary>Full access — user management, settings, all CRUD, IP block actions.</summary>
    Admin = 0,

    /// <summary>Manages apps, releases, artifacts. Cannot manage users or change global settings.</summary>
    Manager = 1,

    /// <summary>Read-only across the entire UI.</summary>
    Viewer = 2,
}
