using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.Entities;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Data;

public static class DatabaseSeeder
{
    private const string SeedPasswordHash = "AQAAAAIAAYagAAAAEOvE3M7p9L1Qk6Fm+Z8xN0Xk1yK3R4jW6Qz7S9pA1B2C3D4E5F6G7H8I9J0K1L2M3N==";

    public static async Task SeedAsync(SecureAccessDbContext context, ILogger logger)
    {
        try
        {
            // Verify if tables are accessible (whether in SQL Server or InMemory)
            if (await context.Roles.AnyAsync())
            {
                logger.LogInformation("Database tables already contain seed data. Skipping initialization.");
                return;
            }

            logger.LogInformation("Database is empty. Populating foundational roles, permissions, and demo identities...");

            // 1. Roles
            var adminRole = new Role
            {
                RoleName = "Admin",
                Description = "Full administrative authority across all PAM configuration and telemetry.",
                CreatedAt = DateTime.UtcNow
            };
            var approverRole = new Role
            {
                RoleName = "Approver",
                Description = "Designated security officer authorized to review, approve, reject, or revoke access requests.",
                CreatedAt = DateTime.UtcNow
            };
            var employeeRole = new Role
            {
                RoleName = "Employee",
                Description = "Standard enterprise identity eligible to submit privileged access requests.",
                CreatedAt = DateTime.UtcNow
            };

            context.Roles.AddRange(adminRole, approverRole, employeeRole);
            await context.SaveChangesAsync();

            // 2. Permissions
            var permissions = new List<Permission>
            {
                new() { PermissionName = Permissions.UsersCreate, Resource = "Users", Description = "Provision new user accounts." },
                new() { PermissionName = Permissions.UsersRead, Resource = "Users", Description = "View user profiles and statuses." },
                new() { PermissionName = Permissions.UsersUpdate, Resource = "Users", Description = "Modify existing user info." },
                new() { PermissionName = Permissions.UsersDeactivate, Resource = "Users", Description = "Deactivate user accounts." },

                new() { PermissionName = Permissions.RolesRead, Resource = "Roles", Description = "Inspect roles and permission hierarchies." },
                new() { PermissionName = Permissions.RolesAssign, Resource = "Roles", Description = "Assign or revoke user roles." },

                new() { PermissionName = Permissions.PermissionsRead, Resource = "Permissions", Description = "Review granular permission catalogs." },

                new() { PermissionName = Permissions.AccessRequestsCreate, Resource = "AccessRequests", Description = "Submit new privileged access requests." },
                new() { PermissionName = Permissions.AccessRequestsViewSelf, Resource = "AccessRequests", Description = "View personal access requests." },
                new() { PermissionName = Permissions.AccessRequestsViewAll, Resource = "AccessRequests", Description = "Inspect all enterprise access requests." },
                new() { PermissionName = Permissions.AccessRequestsApprove, Resource = "AccessRequests", Description = "Grant privileged access requests." },
                new() { PermissionName = Permissions.AccessRequestsReject, Resource = "AccessRequests", Description = "Deny privileged access requests." },
                new() { PermissionName = Permissions.AccessRequestsRevoke, Resource = "AccessRequests", Description = "Immediately revoke active access sessions." },

                new() { PermissionName = Permissions.AuditLogsView, Resource = "AuditLogs", Description = "Inspect security audit records." }
            };

            context.Permissions.AddRange(permissions);
            await context.SaveChangesAsync();

            // 3. Role Permissions
            // Admin gets all permissions
            foreach (var perm in permissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.RoleId,
                    PermissionId = perm.PermissionId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            // Approver gets review, approve, reject, revoke, audit view, users read, roles read, create, view self
            var approverPermNames = new HashSet<string>
            {
                Permissions.AccessRequestsViewAll,
                Permissions.AccessRequestsApprove,
                Permissions.AccessRequestsReject,
                Permissions.AccessRequestsRevoke,
                Permissions.AuditLogsView,
                Permissions.UsersRead,
                Permissions.RolesRead,
                Permissions.AccessRequestsCreate,
                Permissions.AccessRequestsViewSelf
            };

            foreach (var perm in permissions.Where(p => approverPermNames.Contains(p.PermissionName)))
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = approverRole.RoleId,
                    PermissionId = perm.PermissionId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            // Employee gets create and view self
            var employeePermNames = new HashSet<string>
            {
                Permissions.AccessRequestsCreate,
                Permissions.AccessRequestsViewSelf
            };

            foreach (var perm in permissions.Where(p => employeePermNames.Contains(p.PermissionName)))
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = employeeRole.RoleId,
                    PermissionId = perm.PermissionId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();

            // 4. Demo Users
            var adminUser = new User
            {
                FullName = "Sarah Jenkins",
                Username = "admin.sarah",
                Email = "sarah.jenkins@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var approverMarcus = new User
            {
                FullName = "Marcus Vance",
                Username = "approver.marcus",
                Email = "marcus.vance@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var approverElena = new User
            {
                FullName = "Elena Rostova",
                Username = "approver.elena",
                Email = "elena.rostova@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var empDavid = new User
            {
                FullName = "David Chen",
                Username = "emp.david",
                Email = "david.chen@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var empRachel = new User
            {
                FullName = "Rachel Adams",
                Username = "emp.rachel",
                Email = "rachel.adams@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var empCarlos = new User
            {
                FullName = "Carlos Mendez",
                Username = "emp.carlos",
                Email = "carlos.mendez@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var empPriya = new User
            {
                FullName = "Priya Patel",
                Username = "emp.priya",
                Email = "priya.patel@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var empKevin = new User
            {
                FullName = "Kevin Taylor",
                Username = "emp.kevin",
                Email = "kevin.taylor@secureaccess.internal",
                PasswordHash = SeedPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.AddRange(adminUser, approverMarcus, approverElena, empDavid, empRachel, empCarlos, empPriya, empKevin);
            await context.SaveChangesAsync();

            // 5. User Roles
            context.UserRoles.AddRange(
                new UserRole { UserId = adminUser.UserId, RoleId = adminRole.RoleId },
                new UserRole { UserId = adminUser.UserId, RoleId = employeeRole.RoleId },
                new UserRole { UserId = approverMarcus.UserId, RoleId = approverRole.RoleId },
                new UserRole { UserId = approverElena.UserId, RoleId = approverRole.RoleId },
                new UserRole { UserId = empDavid.UserId, RoleId = employeeRole.RoleId },
                new UserRole { UserId = empRachel.UserId, RoleId = employeeRole.RoleId },
                new UserRole { UserId = empCarlos.UserId, RoleId = employeeRole.RoleId },
                new UserRole { UserId = empPriya.UserId, RoleId = employeeRole.RoleId },
                new UserRole { UserId = empKevin.UserId, RoleId = employeeRole.RoleId }
            );

            // 6. Access Requests Demo
            context.AccessRequests.AddRange(
                new AccessRequest
                {
                    RequesterUserId = empDavid.UserId,
                    ResourceName = "SQL-PROD-FINANCE-01",
                    Reason = "Emergency schema patch for quarterly revenue reporting reconciliation.",
                    RequestedAt = DateTime.UtcNow.AddMinutes(-45),
                    Status = "Pending"
                },
                new AccessRequest
                {
                    RequesterUserId = empRachel.UserId,
                    ResourceName = "K8S-PROD-PAYMENTS-CLUSTER",
                    Reason = "Investigate pod crash loop on payment ingress controller pods.",
                    RequestedAt = DateTime.UtcNow.AddMinutes(-30),
                    Status = "Pending"
                },
                new AccessRequest
                {
                    RequesterUserId = empDavid.UserId,
                    ResourceName = "REDIS-PROD-CACHE-CLUSTER",
                    Reason = "Re-indexing invalid session tokens after deployment v2.4.1.",
                    RequestedAt = DateTime.UtcNow.AddDays(-2),
                    StartAt = DateTime.UtcNow.AddDays(-2),
                    ExpiresAt = DateTime.UtcNow.AddDays(-2).AddHours(4),
                    Status = "Approved",
                    ApproverUserId = approverMarcus.UserId,
                    DecisionAt = DateTime.UtcNow.AddDays(-2).AddMinutes(10),
                    DecisionComment = "Approved for 4 hours. Ensure query profiling is active during cache flush."
                },
                new AccessRequest
                {
                    RequesterUserId = empKevin.UserId,
                    ResourceName = "SWIFT-BANKING-TERMINAL",
                    Reason = "Direct access to banking settlement ledger to inspect transaction status.",
                    RequestedAt = DateTime.UtcNow.AddDays(-3),
                    Status = "Rejected",
                    ApproverUserId = approverMarcus.UserId,
                    DecisionAt = DateTime.UtcNow.AddDays(-3).AddMinutes(30),
                    DecisionComment = "Rejected: Settlement terminals require dual-control authorization."
                },
                new AccessRequest
                {
                    RequesterUserId = empCarlos.UserId,
                    ResourceName = "DB-PROD-BILLING-REPLICA",
                    Reason = "Hot-patching replication lag on reporting replica database.",
                    RequestedAt = DateTime.UtcNow.AddDays(-4),
                    StartAt = DateTime.UtcNow.AddDays(-4),
                    ExpiresAt = DateTime.UtcNow.AddDays(-4).AddHours(2),
                    Status = "Revoked",
                    ApproverUserId = approverMarcus.UserId,
                    DecisionAt = DateTime.UtcNow.AddDays(-4).AddHours(2),
                    DecisionComment = "Revoked early: DBA confirmed replication caught up. Session terminated."
                }
            );

            // 7. Audit Logs Demo
            context.AuditLogs.AddRange(
                new AuditLog
                {
                    UserId = adminUser.UserId,
                    Action = "USER_LOGIN_SUCCESS",
                    ResourceType = "Authentication",
                    ResourceId = adminUser.UserId.ToString(),
                    Details = "Admin user authenticated successfully.",
                    IpAddress = "10.200.1.15",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new AuditLog
                {
                    UserId = approverMarcus.UserId,
                    Action = "ACCESS_REQUEST_APPROVED",
                    ResourceType = "AccessRequests",
                    ResourceId = "3",
                    Details = "Approver Marcus Vance approved request for REDIS-PROD-CACHE-CLUSTER.",
                    IpAddress = "10.200.1.22",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            );

            await context.SaveChangesAsync();
            logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}
