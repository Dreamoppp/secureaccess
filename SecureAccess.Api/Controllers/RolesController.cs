using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Roles;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly SecureAccessDbContext _context;

    public RolesController(SecureAccessDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all roles with their assigned granular permissions.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.RolesRead)]
    [ProducesResponseType(typeof(ApiResponse<List<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description,
                CreatedAt = r.CreatedAt,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.PermissionName).ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<RoleDto>>.Ok(roles));
    }
}
