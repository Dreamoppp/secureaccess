namespace SecureAccess.Api.Entities;

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<AccessRequest> RequestedAccesses { get; set; } = new List<AccessRequest>();
    public ICollection<AccessRequest> DecidedAccesses { get; set; } = new List<AccessRequest>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
