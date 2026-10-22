using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/access-requests")]
[Authorize]
public class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestService _requestService;

    public AccessRequestsController(IAccessRequestService requestService)
    {
        _requestService = requestService;
    }

    /// <summary>
    /// Retrieves access requests with status filtering and pagination.
    /// Employees only view their own requests; Approvers and Admins can view all.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccessRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? status = null,
        [FromQuery] int? requesterId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var callerUserId = GetCurrentUserId();
        var canViewAll = User.IsInRole("Admin") ||
                         User.HasClaim("permission", Permissions.AccessRequestsViewAll);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _requestService.GetRequestsAsync(callerUserId, canViewAll, status, requesterId, pageNumber, pageSize);
        return Ok(ApiResponse<PagedResult<AccessRequestDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieves a single access request by ID with authorization checks.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRequestById([FromRoute] int id)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid request ID. Request ID must be a positive integer."));
        }

        var callerUserId = GetCurrentUserId();
        var canViewAll = User.IsInRole("Admin") ||
                         User.HasClaim("permission", Permissions.AccessRequestsViewAll);

        var request = await _requestService.GetRequestByIdAsync(id, callerUserId, canViewAll);
        if (request == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Access request #{id} was not found."));
        }

        return Ok(ApiResponse<AccessRequestDto>.Ok(request));
    }

    /// <summary>
    /// Submits a new time-bounded privileged access request.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.AccessRequestsCreate)]
    [ProducesResponseType(typeof(ApiResponse<AccessRequestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRequest([FromBody] CreateAccessRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var callerUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var created = await _requestService.CreateRequestAsync(callerUserId, dto, ipAddress);
        return CreatedAtAction(nameof(GetRequestById), new { id = created.AccessRequestId },
            ApiResponse<AccessRequestDto>.Ok(created, "Privileged access request submitted successfully."));
    }

    /// <summary>
    /// Approves a pending access request (Enforces self-approval prohibition).
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Permissions.AccessRequestsApprove)]
    [ProducesResponseType(typeof(ApiResponse<AccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveRequest([FromRoute] int id, [FromBody] ApproveAccessRequestDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid request ID. Request ID must be a positive integer."));
        }

        var approverUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var approved = await _requestService.ApproveRequestAsync(id, approverUserId, dto, ipAddress);
        return Ok(ApiResponse<AccessRequestDto>.Ok(approved, $"Access request #{id} approved successfully."));
    }

    /// <summary>
    /// Rejects a pending access request with justification.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Permissions.AccessRequestsReject)]
    [ProducesResponseType(typeof(ApiResponse<AccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectRequest([FromRoute] int id, [FromBody] RejectAccessRequestDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid request ID. Request ID must be a positive integer."));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var approverUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var rejected = await _requestService.RejectRequestAsync(id, approverUserId, dto, ipAddress);
        return Ok(ApiResponse<AccessRequestDto>.Ok(rejected, $"Access request #{id} rejected."));
    }

    /// <summary>
    /// Revokes an active approved access grant before expiry.
    /// </summary>
    [HttpPost("{id:int}/revoke")]
    [Authorize(Policy = Permissions.AccessRequestsRevoke)]
    [ProducesResponseType(typeof(ApiResponse<AccessRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeRequest([FromRoute] int id, [FromBody] RevokeAccessRequestDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid request ID. Request ID must be a positive integer."));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var approverUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var revoked = await _requestService.RevokeRequestAsync(id, approverUserId, dto, ipAddress);
        return Ok(ApiResponse<AccessRequestDto>.Ok(revoked, $"Access request #{id} revoked successfully."));
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idStr, out var id) ? id : 0;
    }
}
