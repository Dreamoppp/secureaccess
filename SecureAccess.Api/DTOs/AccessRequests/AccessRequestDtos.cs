using System.ComponentModel.DataAnnotations;

namespace SecureAccess.Api.DTOs.AccessRequests;

public class AccessRequestDto
{
    public int AccessRequestId { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string RequesterFullName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ApproverUserId { get; set; }
    public string? ApproverUsername { get; set; }
    public string? ApproverFullName { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionComment { get; set; }
    public bool IsActiveGrant { get; set; }
}

public class CreateAccessRequestDto
{
    [Required(ErrorMessage = "Resource Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Resource Name must be between 2 and 100 characters.")]
    public string ResourceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business justification reason is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    public DateTime? StartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ApproveAccessRequestDto
{
    public DateTime? StartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    [StringLength(500, ErrorMessage = "Approval comment cannot exceed 500 characters.")]
    public string? Comment { get; set; }
}

public class RejectAccessRequestDto
{
    [Required(ErrorMessage = "A rejection comment/reason is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Rejection comment must be between 5 and 500 characters.")]
    public string Comment { get; set; } = string.Empty;
}

public class RevokeAccessRequestDto
{
    [Required(ErrorMessage = "A revocation reason is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Revocation comment must be between 5 and 500 characters.")]
    public string Comment { get; set; } = string.Empty;
}
