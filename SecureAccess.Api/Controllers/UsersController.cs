using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Retrieves a paginated directory of users.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.UsersRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _userService.GetUsersAsync(pageNumber, pageSize, isActive, search);
        return Ok(ApiResponse<PagedResult<UserDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieves detailed user profile by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.UsersRead)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserById([FromRoute] int id)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid user ID. User ID must be a positive integer."));
        }

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail($"User with ID #{id} was not found."));
        }
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>
    /// Provisions a new user identity (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.UsersCreate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var currentUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var createdUser = await _userService.CreateUserAsync(dto, currentUserId, ipAddress);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.UserId },
            ApiResponse<UserDto>.Ok(createdUser, "User created successfully."));
    }

    /// <summary>
    /// Updates user contact information.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.UsersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUser([FromRoute] int id, [FromBody] UpdateUserDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid user ID. User ID must be a positive integer."));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var currentUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var updatedUser = await _userService.UpdateUserAsync(id, dto, currentUserId, ipAddress);
        return Ok(ApiResponse<UserDto>.Ok(updatedUser, "User profile updated successfully."));
    }

    /// <summary>
    /// Activates or deactivates a user account.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Permissions.UsersDeactivate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserStatus([FromRoute] int id, [FromBody] UpdateUserStatusDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid user ID. User ID must be a positive integer."));
        }

        var currentUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var updatedUser = await _userService.UpdateUserStatusAsync(id, dto.IsActive, currentUserId, ipAddress);
        return Ok(ApiResponse<UserDto>.Ok(updatedUser, $"User status updated to {(dto.IsActive ? "Active" : "Inactive")}."));
    }

    /// <summary>
    /// Updates role assignments for a user.
    /// </summary>
    [HttpPut("{id:int}/roles")]
    [Authorize(Policy = Permissions.RolesAssign)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserRoles([FromRoute] int id, [FromBody] UpdateUserRolesDto dto)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid user ID. User ID must be a positive integer."));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var currentUserId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var updatedUser = await _userService.UpdateUserRolesAsync(id, dto.RoleIds, currentUserId, ipAddress);
        return Ok(ApiResponse<UserDto>.Ok(updatedUser, "User roles updated successfully."));
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idStr, out var id) ? id : 0;
    }
}
