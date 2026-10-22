namespace SecureAccess.Api.Entities;

public class AccessRequest
{
    public int AccessRequestId { get; set; }
    public int RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;

    public string ResourceName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = "Pending";

    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }

    public DateTime? DecisionAt { get; set; }
    public string? DecisionComment { get; set; }

    /// <summary>
    /// Evaluates if this access request currently represents an active, granted permission window.
    /// </summary>
    public bool IsActiveGrant()
    {
        if (Status != "Approved") return false;
        var now = DateTime.UtcNow;
        if (StartAt.HasValue && now < StartAt.Value) return false;
        if (ExpiresAt.HasValue && now > ExpiresAt.Value) return false;
        return true;
    }
}
