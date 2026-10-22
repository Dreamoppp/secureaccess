-- ============================================================================
-- SecureAccess - Privileged Access Management System
-- Script: 02_SeedData.sql
-- Description: Inserts realistic demo seed data for Users, Roles, UserRoles,
--              Permissions, RolePermissions, AccessRequests, and AuditLogs.
--              Fully idempotent and safe to rerun without duplicating data.
-- Target RDBMS: Microsoft SQL Server 2017+ / Azure SQL Database
-- Execution Order: 2 of 4
-- ============================================================================

USE [SecureAccessDB];
GO

SET NOCOUNT ON;
PRINT 'Starting seed data deployment for SecureAccessDB...';

-- ----------------------------------------------------------------------------
-- 1. Seed Roles
-- Mandatory roles: Admin, Approver, Employee
-- ----------------------------------------------------------------------------
PRINT 'Seeding Roles...';

MERGE INTO dbo.Roles AS target
USING (
    VALUES
        (N'Admin', N'Full administrative authority across all PAM configuration, role mappings, and audit telemetry.', SYSUTCDATETIME()),
        (N'Approver', N'Designated security officer or resource owner authorized to review, approve, reject, or revoke access requests.', SYSUTCDATETIME()),
        (N'Employee', N'Standard enterprise user identity eligible to submit privileged access requests and monitor personal request status.', SYSUTCDATETIME())
) AS source (RoleName, Description, CreatedAt)
ON target.RoleName = source.RoleName
WHEN MATCHED THEN
    UPDATE SET 
        target.Description = source.Description
WHEN NOT MATCHED THEN
    INSERT (RoleName, Description, CreatedAt)
    VALUES (source.RoleName, source.Description, source.CreatedAt);

PRINT 'Roles seeded successfully.';
GO

-- ----------------------------------------------------------------------------
-- 2. Seed Users
-- Identities:
-- - 1 Admin: admin.sarah
-- - 2 Approvers: approver.marcus, approver.elena
-- - 5 Employees: emp.david, emp.rachel, emp.carlos, emp.priya, emp.kevin
--
-- Security Note:
-- Passwords are simulated PBKDF2/HMAC-SHA256 salted cryptographic hash tokens
-- matching ASP.NET Core Identity format. Plain-text passwords are never stored.
-- ----------------------------------------------------------------------------
PRINT 'Seeding Users...';

DECLARE @DemoPasswordHash NVARCHAR(256) = N'AQAAAAIAAYagAAAAEOvE3M7p9L1Qk6Fm+Z8xN0Xk1yK3R4jW6Qz7S9pA1B2C3D4E5F6G7H8I9J0K1L2M3N==';

MERGE INTO dbo.Users AS target
USING (
    VALUES
        -- 1 Admin
        (N'Sarah Jenkins', N'admin.sarah', N'sarah.jenkins@secureaccess.internal', @DemoPasswordHash, 1),
        -- 2 Approvers
        (N'Marcus Vance', N'approver.marcus', N'marcus.vance@secureaccess.internal', @DemoPasswordHash, 1),
        (N'Elena Rostova', N'approver.elena', N'elena.rostova@secureaccess.internal', @DemoPasswordHash, 1),
        -- 5 Employees
        (N'David Chen', N'emp.david', N'david.chen@secureaccess.internal', @DemoPasswordHash, 1),
        (N'Rachel Adams', N'emp.rachel', N'rachel.adams@secureaccess.internal', @DemoPasswordHash, 1),
        (N'Carlos Mendez', N'emp.carlos', N'carlos.mendez@secureaccess.internal', @DemoPasswordHash, 1),
        (N'Priya Patel', N'emp.priya', N'priya.patel@secureaccess.internal', @DemoPasswordHash, 1),
        (N'Kevin Taylor', N'emp.kevin', N'kevin.taylor@secureaccess.internal', @DemoPasswordHash, 1)
) AS source (FullName, Username, Email, PasswordHash, IsActive)
ON target.Username = source.Username
WHEN MATCHED THEN
    UPDATE SET 
        target.FullName = source.FullName,
        target.Email = source.Email,
        target.IsActive = source.IsActive,
        target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (FullName, Username, Email, PasswordHash, IsActive, CreatedAt, UpdatedAt)
    VALUES (source.FullName, source.Username, source.Email, source.PasswordHash, source.IsActive, SYSUTCDATETIME(), SYSUTCDATETIME());

PRINT 'Users seeded successfully.';
GO

-- ----------------------------------------------------------------------------
-- 3. Seed UserRoles (Role Assignments)
-- Assign roles based on principle of least privilege:
-- - admin.sarah -> Admin, Employee
-- - approver.marcus -> Approver
-- - approver.elena -> Approver
-- - emp.* -> Employee
-- ----------------------------------------------------------------------------
PRINT 'Seeding UserRoles...';

INSERT INTO dbo.UserRoles (UserId, RoleId, AssignedAt)
SELECT u.UserId, r.RoleId, SYSUTCDATETIME()
FROM (
    VALUES
        (N'admin.sarah', N'Admin'),
        (N'admin.sarah', N'Employee'),
        (N'approver.marcus', N'Approver'),
        (N'approver.elena', N'Approver'),
        (N'emp.david', N'Employee'),
        (N'emp.rachel', N'Employee'),
        (N'emp.carlos', N'Employee'),
        (N'emp.priya', N'Employee'),
        (N'emp.kevin', N'Employee')
) AS mapping (Username, RoleName)
INNER JOIN dbo.Users u ON u.Username = mapping.Username
INNER JOIN dbo.Roles r ON r.RoleName = mapping.RoleName
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.UserRoles ur
    WHERE ur.UserId = u.UserId AND ur.RoleId = r.RoleId
);

PRINT 'UserRoles seeded successfully.';
GO

-- ----------------------------------------------------------------------------
-- 4. Seed Permissions
-- Granular operations covering:
-- - User management
-- - Role management
-- - Permission management
-- - Access request creation and viewing
-- - Access request approval and rejection
-- - Access revocation
-- - Audit log viewing
-- ----------------------------------------------------------------------------
PRINT 'Seeding Permissions...';

MERGE INTO dbo.Permissions AS target
USING (
    VALUES
        -- User management
        (N'Users.Create', N'Users', N'Provision new user accounts and identities into SecureAccess.', SYSUTCDATETIME()),
        (N'Users.Read', N'Users', N'View user profiles, directory listings, and activation statuses.', SYSUTCDATETIME()),
        (N'Users.Update', N'Users', N'Modify existing user information and contact details.', SYSUTCDATETIME()),
        (N'Users.Deactivate', N'Users', N'Deactivate or lock user accounts from requesting access.', SYSUTCDATETIME()),

        -- Role management
        (N'Roles.Read', N'Roles', N'Inspect available roles and their permission hierarchies.', SYSUTCDATETIME()),
        (N'Roles.Assign', N'Roles', N'Assign or revoke roles for enterprise users.', SYSUTCDATETIME()),

        -- Permission management
        (N'Permissions.Read', N'Permissions', N'Review granular security permission catalogs and mappings.', SYSUTCDATETIME()),

        -- Access request creation and viewing
        (N'AccessRequests.Create', N'AccessRequests', N'Submit new just-in-time privileged access requests.', SYSUTCDATETIME()),
        (N'AccessRequests.ViewSelf', N'AccessRequests', N'View personal access requests and decision histories.', SYSUTCDATETIME()),
        (N'AccessRequests.ViewAll', N'AccessRequests', N'Inspect all enterprise access requests across departments.', SYSUTCDATETIME()),

        -- Access request approval and rejection
        (N'AccessRequests.Approve', N'AccessRequests', N'Grant privileged access requests and establish expiry windows.', SYSUTCDATETIME()),
        (N'AccessRequests.Reject', N'AccessRequests', N'Deny privileged access requests with justification notes.', SYSUTCDATETIME()),

        -- Access revocation
        (N'AccessRequests.Revoke', N'AccessRequests', N'Immediately revoke active privileged access sessions.', SYSUTCDATETIME()),

        -- Audit log viewing
        (N'AuditLogs.View', N'AuditLogs', N'Inspect security audit records, access history, and compliance logs.', SYSUTCDATETIME())
) AS source (PermissionName, Resource, Description, CreatedAt)
ON target.PermissionName = source.PermissionName
WHEN MATCHED THEN
    UPDATE SET
        target.Resource = source.Resource,
        target.Description = source.Description
WHEN NOT MATCHED THEN
    INSERT (PermissionName, Resource, Description, CreatedAt)
    VALUES (source.PermissionName, source.Resource, source.Description, source.CreatedAt);

PRINT 'Permissions seeded successfully.';
GO

-- ----------------------------------------------------------------------------
-- 5. Seed RolePermissions (Mapping Permissions to Roles)
-- - Admin: All 14 permissions
-- - Approver: AccessRequests.ViewAll, AccessRequests.Approve, AccessRequests.Reject,
--             AccessRequests.Revoke, AuditLogs.View, Users.Read, Roles.Read,
--             AccessRequests.Create, AccessRequests.ViewSelf
-- - Employee: AccessRequests.Create, AccessRequests.ViewSelf
-- ----------------------------------------------------------------------------
PRINT 'Seeding RolePermissions...';

-- Admin Role Mappings (All permissions)
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AssignedAt)
SELECT r.RoleId, p.PermissionId, SYSUTCDATETIME()
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.RoleName = N'Admin'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

-- Approver Role Mappings
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AssignedAt)
SELECT r.RoleId, p.PermissionId, SYSUTCDATETIME()
FROM dbo.Roles r
INNER JOIN dbo.Permissions p ON p.PermissionName IN (
    N'AccessRequests.ViewAll',
    N'AccessRequests.Approve',
    N'AccessRequests.Reject',
    N'AccessRequests.Revoke',
    N'AuditLogs.View',
    N'Users.Read',
    N'Roles.Read',
    N'AccessRequests.Create',
    N'AccessRequests.ViewSelf'
)
WHERE r.RoleName = N'Approver'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

-- Employee Role Mappings
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AssignedAt)
SELECT r.RoleId, p.PermissionId, SYSUTCDATETIME()
FROM dbo.Roles r
INNER JOIN dbo.Permissions p ON p.PermissionName IN (
    N'AccessRequests.Create',
    N'AccessRequests.ViewSelf'
)
WHERE r.RoleName = N'Employee'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

PRINT 'RolePermissions seeded successfully.';
GO

-- ----------------------------------------------------------------------------
-- 6. Seed AccessRequests
-- Minimum required: 10 access requests across all statuses:
-- - Pending: 3 requests
-- - Approved: 4 requests
-- - Rejected: 3 requests
-- - Revoked: 2 requests
-- Total: 12 realistic access requests
-- ----------------------------------------------------------------------------
PRINT 'Seeding AccessRequests...';

-- Resolve User IDs for reference
DECLARE @AdminId INT        = (SELECT UserId FROM dbo.Users WHERE Username = N'admin.sarah');
DECLARE @MarcusId INT       = (SELECT UserId FROM dbo.Users WHERE Username = N'approver.marcus');
DECLARE @ElenaId INT        = (SELECT UserId FROM dbo.Users WHERE Username = N'approver.elena');
DECLARE @DavidId INT        = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.david');
DECLARE @RachelId INT       = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.rachel');
DECLARE @CarlosId INT       = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.carlos');
DECLARE @PriyaId INT        = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.priya');
DECLARE @KevinId INT        = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.kevin');

-- Define reference timestamps
DECLARE @BaseTime DATETIME2(7) = SYSUTCDATETIME();

-- Only insert demo requests if AccessRequests is currently empty
IF NOT EXISTS (SELECT 1 FROM dbo.AccessRequests)
BEGIN
    INSERT INTO dbo.AccessRequests 
    (
        RequesterUserId, 
        ResourceName, 
        Reason, 
        RequestedAt, 
        StartAt, 
        ExpiresAt, 
        Status, 
        ApproverUserId, 
        DecisionAt, 
        DecisionComment
    )
    VALUES
        -- 1. Pending Request - David Chen
        (@DavidId, N'SQL-PROD-FINANCE-01', N'Emergency schema patch for quarterly revenue reporting reconciliation.', 
         DATEADD(MINUTE, -45, @BaseTime), NULL, NULL, 
         N'Pending', NULL, NULL, NULL),

        -- 2. Pending Request - Rachel Adams
        (@RachelId, N'K8S-PROD-PAYMENTS-CLUSTER', N'Investigate pod crash loop on payment ingress controller pods.', 
         DATEADD(MINUTE, -30, @BaseTime), NULL, NULL, 
         N'Pending', NULL, NULL, NULL),

        -- 3. Pending Request - Carlos Mendez
        (@CarlosId, N'AWS-KMS-PAYMENT-KEYS', N'Annual hardware security module key rotation and verification run.', 
         DATEADD(MINUTE, -15, @BaseTime), NULL, NULL, 
         N'Pending', NULL, NULL, NULL),

        -- 4. Approved Request - David Chen (Approved by Marcus Vance)
        (@DavidId, N'REDIS-PROD-CACHE-CLUSTER', N'Re-indexing invalid session tokens after deployment v2.4.1.', 
         DATEADD(DAY, -2, @BaseTime), DATEADD(DAY, -2, @BaseTime), DATEADD(HOUR, 4, DATEADD(DAY, -2, @BaseTime)), 
         N'Approved', @MarcusId, DATEADD(MINUTE, 10, DATEADD(DAY, -2, @BaseTime)), 
         N'Approved for 4 hours. Ensure query profiling is active during cache flush.'),

        -- 5. Approved Request - Rachel Adams (Approved by Elena Rostova)
        (@RachelId, N'FIREWALL-EDGE-GATEWAY', N'Temporary port opening for external audit compliance scanning.', 
         DATEADD(DAY, -1, @BaseTime), DATEADD(DAY, -1, @BaseTime), DATEADD(HOUR, 2, DATEADD(DAY, -1, @BaseTime)), 
         N'Approved', @ElenaId, DATEADD(MINUTE, 15, DATEADD(DAY, -1, @BaseTime)), 
         N'Approved for designated third-party auditor CIDR block only.'),

        -- 6. Approved Request - Priya Patel (Approved by Marcus Vance)
        (@PriyaId, N'PROD-ELK-LOG-ANALYTICS', N'End-to-end trace validation of asynchronous payment webhooks.', 
         DATEADD(HOUR, -6, @BaseTime), DATEADD(HOUR, -6, @BaseTime), DATEADD(HOUR, 2, @BaseTime), 
         N'Approved', @MarcusId, DATEADD(MINUTE, 20, DATEADD(HOUR, -6, @BaseTime)), 
         N'Approved read-only access for debugging test suite.'),

        -- 7. Approved Request - Kevin Taylor (Approved by Elena Rostova)
        (@KevinId, N'AD-AZURE-SYNC-AGENT', N'Re-synchronize stalled directory federation services.', 
         DATEADD(HOUR, -3, @BaseTime), DATEADD(HOUR, -3, @BaseTime), DATEADD(HOUR, 1, @BaseTime), 
         N'Approved', @ElenaId, DATEADD(MINUTE, 8, DATEADD(HOUR, -3, @BaseTime)), 
         N'Approved with requirement to post event log diagnostics to ticket SEC-4029.'),

        -- 8. Rejected Request - Kevin Taylor (Rejected by Marcus Vance)
        (@KevinId, N'SWIFT-BANKING-TERMINAL', N'Direct access to banking settlement ledger to inspect transaction status.', 
         DATEADD(DAY, -3, @BaseTime), NULL, NULL, 
         N'Rejected', @MarcusId, DATEADD(MINUTE, 30, DATEADD(DAY, -3, @BaseTime)), 
         N'Rejected: Settlement terminals require dual-control authorization and Level 3 finance certification.'),

        -- 9. Rejected Request - David Chen (Rejected by Elena Rostova)
        (@DavidId, N'CORE-ROUTER-BGP-CONFIG', N'Modify border routing table to test latency routing optimization.', 
         DATEADD(DAY, -2, @BaseTime), NULL, NULL, 
         N'Rejected', @ElenaId, DATEADD(MINUTE, 45, DATEADD(DAY, -2, @BaseTime)), 
         N'Rejected: Core routing modifications must be submitted via Network CAB with formal rollback plan.'),

        -- 10. Rejected Request - Priya Patel (Rejected by Marcus Vance)
        (@PriyaId, N'PROD-PII-ENCRYPTION-VAULT', N'Manual decryption of customer records for QA test case reproduction.', 
         DATEADD(DAY, -1, @BaseTime), NULL, NULL, 
         N'Rejected', @MarcusId, DATEADD(MINUTE, 12, DATEADD(DAY, -1, @BaseTime)), 
         N'Rejected: Live PII vault access is prohibited for test reproduction. Use sanitized staging environment.'),

        -- 11. Revoked Request - Carlos Mendez (Approved by Elena, Revoked by Marcus)
        (@CarlosId, N'DB-PROD-BILLING-REPLICA', N'Hot-patching replication lag on reporting replica database.', 
         DATEADD(DAY, -4, @BaseTime), DATEADD(DAY, -4, @BaseTime), DATEADD(HOUR, 8, DATEADD(DAY, -4, @BaseTime)), 
         N'Revoked', @MarcusId, DATEADD(HOUR, 2, DATEADD(DAY, -4, @BaseTime)), 
         N'Revoked early: DBA confirmed replication caught up at 04:30 UTC. Access terminated per security protocol.'),

        -- 12. Revoked Request - Rachel Adams (Approved by Marcus, Revoked by Elena)
        (@RachelId, N'PROD-SSH-BASTION-01', N'Emergency node restart following kernel panic notification.', 
         DATEADD(DAY, -2, @BaseTime), DATEADD(DAY, -2, @BaseTime), DATEADD(HOUR, 6, DATEADD(DAY, -2, @BaseTime)), 
         N'Revoked', @ElenaId, DATEADD(HOUR, 1, DATEADD(DAY, -2, @BaseTime)), 
         N'Revoked early: Maintenance window completed ahead of schedule.');

    PRINT '12 AccessRequests seeded successfully.';
END
ELSE
BEGIN
    PRINT 'AccessRequests table already populated. Skipping insertion.';
END
GO

-- ----------------------------------------------------------------------------
-- 7. Seed AuditLogs
-- Covers security events:
-- - User authentication (success/fail)
-- - Access request creation
-- - Access request approval
-- - Access request rejection
-- - Access revocation
-- - Role assignment
-- - User administration
-- ----------------------------------------------------------------------------
PRINT 'Seeding AuditLogs...';

DECLARE @AdminId INT   = (SELECT UserId FROM dbo.Users WHERE Username = N'admin.sarah');
DECLARE @MarcusId INT  = (SELECT UserId FROM dbo.Users WHERE Username = N'approver.marcus');
DECLARE @ElenaId INT   = (SELECT UserId FROM dbo.Users WHERE Username = N'approver.elena');
DECLARE @DavidId INT   = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.david');
DECLARE @RachelId INT  = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.rachel');
DECLARE @CarlosId INT  = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.carlos');
DECLARE @KevinId INT   = (SELECT UserId FROM dbo.Users WHERE Username = N'emp.kevin');
DECLARE @BaseTime DATETIME2(7) = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM dbo.AuditLogs)
BEGIN
    INSERT INTO dbo.AuditLogs (UserId, Action, ResourceType, ResourceId, Details, IpAddress, CreatedAt)
    VALUES
        -- Authentication Audits
        (@AdminId, N'USER_LOGIN_SUCCESS', N'Authentication', CAST(@AdminId AS NVARCHAR(100)), 
         N'Admin user authenticated successfully via WinForms Desktop Client with MFA validation.', 
         N'10.200.1.15', DATEADD(DAY, -5, @BaseTime)),

        (NULL, N'USER_LOGIN_FAILED', N'Authentication', N'unknown_account', 
         N'Failed authentication attempt: Username does not exist in identity directory.', 
         N'192.168.10.88', DATEADD(DAY, -5, @BaseTime)),

        (@MarcusId, N'USER_LOGIN_SUCCESS', N'Authentication', CAST(@MarcusId AS NVARCHAR(100)), 
         N'Approver authenticated successfully via desktop client.', 
         N'10.200.1.22', DATEADD(DAY, -4, @BaseTime)),

        -- Role Assignment Audits
        (@AdminId, N'ROLE_ASSIGNED_TO_USER', N'UserRoles', N'admin.sarah:Admin', 
         N'System bootstrap: Initial administrative role assigned to primary PAM administrator.', 
         N'127.0.0.1', DATEADD(DAY, -5, @BaseTime)),

        (@AdminId, N'ROLE_ASSIGNED_TO_USER', N'UserRoles', N'approver.marcus:Approver', 
         N'Lead security officer granted Approver role privileges.', 
         N'10.200.1.15', DATEADD(DAY, -5, @BaseTime)),

        -- Access Request Creation Audits
        (@DavidId, N'ACCESS_REQUEST_CREATED', N'AccessRequests', N'1', 
         N'David Chen submitted access request for resource: SQL-PROD-FINANCE-01.', 
         N'10.200.2.40', DATEADD(MINUTE, -45, @BaseTime)),

        (@RachelId, N'ACCESS_REQUEST_CREATED', N'AccessRequests', N'2', 
         N'Rachel Adams submitted access request for resource: K8S-PROD-PAYMENTS-CLUSTER.', 
         N'10.200.2.45', DATEADD(MINUTE, -30, @BaseTime)),

        -- Approval Audits
        (@MarcusId, N'ACCESS_REQUEST_APPROVED', N'AccessRequests', N'4', 
         N'Marcus Vance approved access request #4 for REDIS-PROD-CACHE-CLUSTER. Duration: 4 hours.', 
         N'10.200.1.22', DATEADD(DAY, -2, @BaseTime)),

        (@ElenaId, N'ACCESS_REQUEST_APPROVED', N'AccessRequests', N'5', 
         N'Elena Rostova approved access request #5 for FIREWALL-EDGE-GATEWAY. Duration: 2 hours.', 
         N'10.200.1.33', DATEADD(DAY, -1, @BaseTime)),

        -- Rejection Audits
        (@MarcusId, N'ACCESS_REQUEST_REJECTED', N'AccessRequests', N'8', 
         N'Marcus Vance rejected access request #8 for SWIFT-BANKING-TERMINAL. Reason: Policy SEC-FIN-01 non-compliance.', 
         N'10.200.1.22', DATEADD(DAY, -3, @BaseTime)),

        (@ElenaId, N'ACCESS_REQUEST_REJECTED', N'AccessRequests', N'9', 
         N'Elena Rostova rejected access request #9 for CORE-ROUTER-BGP-CONFIG. Reason: Requires CAB change review.', 
         N'10.200.1.33', DATEADD(DAY, -2, @BaseTime)),

        -- Revocation Audits
        (@MarcusId, N'ACCESS_REQUEST_REVOKED', N'AccessRequests', N'11', 
         N'Marcus Vance initiated early session revocation for request #11 on DB-PROD-BILLING-REPLICA. Session terminated.', 
         N'10.200.1.22', DATEADD(DAY, -4, @BaseTime)),

        (@ElenaId, N'ACCESS_REQUEST_REVOKED', N'AccessRequests', N'12', 
         N'Elena Rostova initiated early session revocation for request #12 on PROD-SSH-BASTION-01. Maintenance finished.', 
         N'10.200.1.33', DATEADD(DAY, -2, @BaseTime)),

        -- Administrative Configuration Audit
        (@AdminId, N'USER_UPDATED', N'Users', CAST(@KevinId AS NVARCHAR(100)), 
         N'Administrator Sarah Jenkins reviewed and confirmed account activation for Kevin Taylor.', 
         N'10.200.1.15', DATEADD(DAY, -3, @BaseTime));

    PRINT '14 AuditLogs seeded successfully.';
END
ELSE
BEGIN
    PRINT 'AuditLogs table already populated. Skipping insertion.';
END
GO

PRINT 'Seed data deployment completed successfully.';
GO
