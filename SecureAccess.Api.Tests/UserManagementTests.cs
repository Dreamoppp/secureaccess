using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.Api.Entities;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Tests;

public class UserManagementTests
{
    private SecureAccessDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SecureAccessDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new SecureAccessDbContext(options);

        if (!context.Roles.Any())
        {
            context.Roles.AddRange(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Approver" },
                new Role { RoleId = 3, RoleName = "Employee" }
            );
            context.SaveChanges();
        }

        return context;
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(CreateUser_DuplicateUsername_ThrowsInvalidOperationException));
        var hasher = new PasswordHasherService();
        var audit = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var userService = new UserService(context, hasher, audit);

        context.Users.Add(new User
        {
            UserId = 1,
            Username = "admin.sarah",
            FullName = "Sarah Jenkins",
            Email = "sarah.jenkins@test.internal",
            PasswordHash = "dummy",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var dto = new CreateUserDto
        {
            Username = "admin.sarah", // Duplicate
            FullName = "Another Sarah",
            Email = "another.sarah@test.internal",
            Password = "Password123!"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.CreateUserAsync(dto, 1, "127.0.0.1"));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public async Task UpdateUserStatus_DeactivatesAccountAndLogsAudit()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(UpdateUserStatus_DeactivatesAccountAndLogsAudit));
        var hasher = new PasswordHasherService();
        var audit = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var userService = new UserService(context, hasher, audit);

        var user = new User
        {
            UserId = 5,
            Username = "emp.kevin",
            FullName = "Kevin Taylor",
            Email = "kevin.taylor@test.internal",
            PasswordHash = "dummy",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        var result = await userService.UpdateUserStatusAsync(5, false, 1, "127.0.0.1");

        // Assert
        Assert.False(result.IsActive);

        var updated = await context.Users.FindAsync(5);
        Assert.False(updated!.IsActive);

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "USER_DEACTIVATED");
        Assert.NotNull(auditLog);
        Assert.Equal("5", auditLog.ResourceId);
    }

    [Fact]
    public async Task UpdateUserRoles_SelfModification_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(UpdateUserRoles_SelfModification_ThrowsInvalidOperationException));
        var hasher = new PasswordHasherService();
        var audit = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var userService = new UserService(context, hasher, audit);

        var adminUser = new User
        {
            UserId = 1,
            Username = "admin.sarah",
            FullName = "Sarah Jenkins",
            Email = "sarah.jenkins@test.internal",
            PasswordHash = "dummy",
            IsActive = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act & Assert: Admin #1 attempting to modify their own roles
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.UpdateUserRolesAsync(id: 1, roleIds: new List<int> { 1, 2 }, updaterUserId: 1, ipAddress: "127.0.0.1"));

        Assert.Contains("Privilege Separation Violation", ex.Message);
    }

    [Fact]
    public async Task UpdateUserRoles_EmptyRoleList_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(UpdateUserRoles_EmptyRoleList_ThrowsArgumentException));
        var hasher = new PasswordHasherService();
        var audit = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var userService = new UserService(context, hasher, audit);

        var user = new User
        {
            UserId = 3,
            Username = "emp.rachel",
            FullName = "Rachel Adams",
            Email = "rachel.adams@test.internal",
            PasswordHash = "dummy",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act & Assert: Assigning empty role list
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            userService.UpdateUserRolesAsync(id: 3, roleIds: new List<int>(), updaterUserId: 1, ipAddress: "127.0.0.1"));

        Assert.Contains("At least one valid role", ex.Message);
    }

    [Fact]
    public async Task UpdateUserStatus_SelfDeactivation_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(UpdateUserStatus_SelfDeactivation_ThrowsInvalidOperationException));
        var hasher = new PasswordHasherService();
        var audit = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var userService = new UserService(context, hasher, audit);

        var adminUser = new User
        {
            UserId = 1,
            Username = "admin.sarah",
            FullName = "Sarah Jenkins",
            Email = "sarah.jenkins@test.internal",
            PasswordHash = "dummy",
            IsActive = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act & Assert: Admin #1 attempting to deactivate themselves
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.UpdateUserStatusAsync(id: 1, isActive: false, updaterUserId: 1, ipAddress: "127.0.0.1"));

        Assert.Contains("Safety Guard", ex.Message);
    }
}
