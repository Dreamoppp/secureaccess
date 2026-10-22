using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.DTOs.AuditLogs;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Inspects immutable security audit records and event history.
    /// Access is restricted to Admins and authorized security auditors.
    /// Notice: No modification or deletion endpoints exist by design.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.AuditLogsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _auditLogService.GetAuditLogsAsync(pageNumber, pageSize, action, resourceType);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}
