namespace ZeroTrust.Backend.Models;

/// <summary>
/// Response DTO for current user information endpoint
/// Includes user details, roles, and organization information
/// Used for frontend to determine UI capabilities and JIT provisioning test scenarios
/// </summary>
public class CurrentUserResponse
{
    /// <summary>
    /// User ID (internal UUID)
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// User's display name
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Identity Provider name (e.g., "IDP-A", "IDP-B")
    /// </summary>
    public string? IdentityProvider { get; set; }

    /// <summary>
    /// Organization ID
    /// </summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// Organization name
    /// </summary>
    public string OrganizationName { get; set; } = null!;

    /// <summary>
    /// List of roles assigned to the user
    /// Can be: Requester, DataOwner, Admin, Auditor
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// User's current status (Active, Inactive, Suspended)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Timestamp of user's first login (JIT provisioning time)
    /// Useful for testing JIT scenarios - shows when the user was auto-provisioned
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of user's last access
    /// Useful for testing JIT provisioning with new users
    /// </summary>
    public DateTime LastAccessAt { get; set; }

    /// <summary>
    /// Whether this is a newly provisioned user (created in this session)
    /// Useful for smoke tests to validate JIT provisioning worked
    /// </summary>
    public bool IsNewlyProvisioned { get; set; }
}
