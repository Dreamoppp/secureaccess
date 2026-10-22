using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.Api.Entities;

namespace SecureAccess.Api.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 20, bool? isActive = null, string? search = null);
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto, int creatorUserId, string? ipAddress);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto, int updaterUserId, string? ipAddress);
    Task<UserDto> UpdateUserStatusAsync(int id, bool isActive, int updaterUserId, string? ipAddress);
    Task<UserDto> UpdateUserRolesAsync(int id, List<int> roleIds, int updaterUserId, string? ipAddress);
}

public class UserService : IUserService
{
    private readonly SecureAccessDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public UserService(
        SecureAccessDbContext context,
        IPasswordHasherService passwordHasher,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(
        int pageNumber = 1,
        int pageSize = 20,
        bool? isActive = null,
        string? search = null)
    {
        var query = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(search)
                                  || u.FullName.ToLower().Contains(search)
                                  || u.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(u => u.UserId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => MapToDto(u))
            .ToListAsync();

        return new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == id);

        return user == null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto, int creatorUserId, string? ipAddress)
    {
        var usernameTrimmed = dto.Username.Trim();
        var emailTrimmed = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Username == usernameTrimmed))
        {
            throw new InvalidOperationException($"Username '{usernameTrimmed}' is already registered.");
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailTrimmed))
        {
            throw new InvalidOperationException($"Email address '{emailTrimmed}' is already registered.");
        }

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Username = usernameTrimmed,
            Email = emailTrimmed,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assign roles (default to Employee if none provided)
        var roleIds = dto.RoleIds.Distinct().ToList();
        if (roleIds.Count == 0)
        {
            var employeeRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
            if (employeeRole != null)
            {
                roleIds.Add(employeeRole.RoleId);
            }
        }

        foreach (var roleId in roleIds)
        {
            if (await _context.Roles.AnyAsync(r => r.RoleId == roleId))
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            creatorUserId,
            "USER_CREATED",
            "Users",
            user.UserId.ToString(),
            $"User account '{user.Username}' ({user.Email}) created with {roleIds.Count} role(s).",
            ipAddress);

        return (await GetUserByIdAsync(user.UserId))!;
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto, int updaterUserId, string? ipAddress)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID #{id} was not found.");
        }

        var emailTrimmed = dto.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailTrimmed && u.UserId != id))
        {
            throw new InvalidOperationException($"Email address '{emailTrimmed}' is already used by another account.");
        }

        user.FullName = dto.FullName.Trim();
        user.Email = emailTrimmed;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            updaterUserId,
            "USER_UPDATED",
            "Users",
            id.ToString(),
            $"User profile updated for '{user.Username}'. FullName: {user.FullName}, Email: {user.Email}.",
            ipAddress);

        return (await GetUserByIdAsync(id))!;
    }

    public async Task<UserDto> UpdateUserStatusAsync(int id, bool isActive, int updaterUserId, string? ipAddress)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID #{id} was not found.");
        }

        // Security Guard: Prevent administrator from deactivating their own active account
        if (id == updaterUserId && !isActive)
        {
            throw new InvalidOperationException("Safety Guard: You cannot deactivate your own active user account.");
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            updaterUserId,
            isActive ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            "Users",
            id.ToString(),
            $"User account '{user.Username}' status changed to {(isActive ? "Active" : "Inactive")}.",
            ipAddress);

        return (await GetUserByIdAsync(id))!;
    }

    public async Task<UserDto> UpdateUserRolesAsync(int id, List<int> roleIds, int updaterUserId, string? ipAddress)
    {
        // Security Guard: Separation of duties prevents users from altering their own role assignments
        if (id == updaterUserId)
        {
            throw new InvalidOperationException("Privilege Separation Violation: Users are not permitted to modify their own role assignments.");
        }

        if (roleIds == null || roleIds.Count == 0)
        {
            throw new ArgumentException("At least one valid role must be assigned to the user.");
        }

        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID #{id} was not found.");
        }

        // Remove existing roles
        _context.UserRoles.RemoveRange(user.UserRoles);

        // Add new unique roles
        foreach (var roleId in roleIds.Distinct())
        {
            if (await _context.Roles.AnyAsync(r => r.RoleId == roleId))
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = id,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            updaterUserId,
            "ROLE_ASSIGNED_TO_USER",
            "UserRoles",
            id.ToString(),
            $"Updated role assignments for user '{user.Username}'. New role count: {roleIds.Count}.",
            ipAddress);

        return (await GetUserByIdAsync(id))!;
    }

    private static UserDto MapToDto(User u)
    {
        return new UserDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Username = u.Username,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            Roles = u.UserRoles.Select(ur => ur.Role.RoleName).ToList()
        };
    }
}
