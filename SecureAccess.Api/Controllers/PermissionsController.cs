using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Permissions;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly SecureAccessDbContext _context;

    public PermissionsController(SecureAccessDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves the complete catalog of securable permissions.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.PermissionsRead)]
    [ProducesResponseType(typeof(ApiResponse<List<PermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Resource)
                .ThenBy(p => p.PermissionName)
            .Select(p => new PermissionDto
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                Resource = p.Resource,
                Description = p.Description,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<PermissionDto>>.Ok(permissions));
    }
}
