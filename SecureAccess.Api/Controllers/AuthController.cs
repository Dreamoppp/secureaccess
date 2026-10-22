using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.Auth;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SecureAccessDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogService;

    public AuthController(
        SecureAccessDbContext context,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Authenticates user credentials and issues a signed JWT access token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim());

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // Log security event for failed login without storing entered password
            await _auditLogService.LogAsync(
                user?.UserId,
                "USER_LOGIN_FAILED",
                "Authentication",
                request.Username.Trim(),
                $"Failed authentication attempt for username '{request.Username.Trim()}'.",
                ipAddress);

            return Unauthorized(ApiResponse<object>.Fail("Invalid username or password."));
        }

        if (!user.IsActive)
        {
            await _auditLogService.LogAsync(
                user.UserId,
                "USER_LOGIN_DENIED_INACTIVE",
                "Authentication",
                user.UserId.ToString(),
                $"Login blocked for deactivated account '{user.Username}'.",
                ipAddress);

            return Unauthorized(ApiResponse<object>.Fail("Account is inactive. Please contact the administrator."));
        }

        // Extract assigned roles
        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();

        // Extract distinct permissions through RolePermissions
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionName)
            .Distinct()
            .ToList();

        // Generate JWT
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles, permissions);

        // Audit successful login
        await _auditLogService.LogAsync(
            user.UserId,
            "USER_LOGIN_SUCCESS",
            "Authentication",
            user.UserId.ToString(),
            $"User '{user.Username}' authenticated successfully via PAM API.",
            ipAddress);

        var responseDto = new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = roles
            }
        };

        return Ok(ApiResponse<LoginResponseDto>.Ok(responseDto, "Authentication successful."));
    }

    /// <summary>
    /// Retrieves current authenticated identity, roles, and granular permissions.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid user token claims."));
        }

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null || !user.IsActive)
        {
            return Unauthorized(ApiResponse<object>.Fail("User account not found or has been deactivated."));
        }

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionName)
            .Distinct()
            .ToList();

        var currentUser = new CurrentUserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            IsActive = user.IsActive,
            Roles = roles,
            Permissions = permissions
        };

        return Ok(ApiResponse<CurrentUserDto>.Ok(currentUser));
    }
}
