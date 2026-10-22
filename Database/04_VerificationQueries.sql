-- ============================================================================
-- SecureAccess - Privileged Access Management System
-- Script: 04_VerificationQueries.sql
-- Description: Automated verification script validating schema structure,
--              record counts, relational mappings, RBAC resolution, and
--              constraint enforcement via safe transactional negative tests.
-- Target RDBMS: Microsoft SQL Server 2017+ / Azure SQL Database
-- Execution Order: 4 of 4
-- ============================================================================

USE [SecureAccessDB];
GO

SET NOCOUNT ON;
PRINT '============================================================================';
PRINT 'SECUREACCESS DATABASE VERIFICATION SUITE';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- SECTION 1: Schema Verification
-- Verifies the existence of all 7 core and junction tables.
-- ============================================================================
PRINT '--- SECTION 1: VERIFYING TABLE EXISTENCE ---';

SELECT 
    t.name AS TableName,
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.create_date AS CreatedDate,
    p.rows AS ApproxRowCount
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE t.name IN (
    N'Users', 
    N'Roles', 
    N'UserRoles', 
    N'Permissions', 
    N'RolePermissions', 
    N'AccessRequests', 
    N'AuditLogs'
)
ORDER BY t.name;

-- Verify 7 tables exist
DECLARE @TableCount INT = (
    SELECT COUNT(*) 
    FROM sys.tables 
    WHERE name IN (N'Users', N'Roles', N'UserRoles', N'Permissions', N'RolePermissions', N'AccessRequests', N'AuditLogs')
);

IF @TableCount = 7
    PRINT 'PASS [Schema]: All 7 required tables exist in SecureAccessDB.';
ELSE
    PRINT 'FAIL [Schema]: Expected 7 tables, found ' + CAST(@TableCount AS VARCHAR(10));
PRINT '';

-- ============================================================================
-- SECTION 2: Seed Record Counts
-- Verifies that demo data meets or exceeds all project requirements.
-- ============================================================================
PRINT '--- SECTION 2: VERIFYING SEED DATA VOLUME ---';

SELECT 'Users' AS EntityName, COUNT(*) AS RecordCount FROM dbo.Users
UNION ALL
SELECT 'Roles', COUNT(*) FROM dbo.Roles
UNION ALL
SELECT 'UserRoles', COUNT(*) FROM dbo.UserRoles
UNION ALL
SELECT 'Permissions', COUNT(*) FROM dbo.Permissions
UNION ALL
SELECT 'RolePermissions', COUNT(*) FROM dbo.RolePermissions
UNION ALL
SELECT 'AccessRequests', COUNT(*) FROM dbo.AccessRequests
UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM dbo.AuditLogs;
PRINT '';

-- ============================================================================
-- SECTION 3: Relational Mappings & RBAC Queries
-- ============================================================================
PRINT '--- SECTION 3A: USER TO ROLE MAPPINGS ---';

SELECT 
    u.UserId,
    u.Username,
    u.FullName,
    u.Email,
    u.IsActive,
    r.RoleName,
    ur.AssignedAt AS RoleAssignedAt
FROM dbo.Users u
INNER JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
INNER JOIN dbo.Roles r ON ur.RoleId = r.RoleId
ORDER BY u.UserId, r.RoleName;
PRINT '';

PRINT '--- SECTION 3B: ROLE TO PERMISSION MAPPINGS ---';

SELECT 
    r.RoleName,
    p.PermissionName,
    p.Resource,
    p.Description
FROM dbo.Roles r
INNER JOIN dbo.RolePermissions rp ON r.RoleId = rp.RoleId
INNER JOIN dbo.Permissions p ON rp.PermissionId = p.PermissionId
ORDER BY r.RoleName, p.Resource, p.PermissionName;
PRINT '';

PRINT '--- SECTION 3C: EFFECTIVE USER PERMISSION MATRIX (PAM Runtime Resolution) ---';
-- Demonstrates how ASP.NET Core API / WinForms client determines user rights
SELECT 
    u.Username,
    u.FullName,
    r.RoleName,
    p.PermissionName,
    p.Resource
FROM dbo.Users u
INNER JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
INNER JOIN dbo.Roles r ON ur.RoleId = r.RoleId
INNER JOIN dbo.RolePermissions rp ON r.RoleId = rp.RoleId
INNER JOIN dbo.Permissions p ON rp.PermissionId = p.PermissionId
ORDER BY u.Username, p.PermissionName;
PRINT '';

-- ============================================================================
-- SECTION 4: Access Request Workflow Queries
-- ============================================================================
PRINT '--- SECTION 4A: ACCESS REQUESTS BY STATUS SUMMARY ---';

SELECT 
    Status, 
    COUNT(*) AS TotalRequests,
    MIN(RequestedAt) AS OldestRequestUtc,
    MAX(RequestedAt) AS NewestRequestUtc
FROM dbo.AccessRequests
GROUP BY Status
ORDER BY Status;
PRINT '';

PRINT '--- SECTION 4B: FILTER REQUESTS BY STATUS (Pending Approver Triage Queue) ---';

SELECT 
    ar.AccessRequestId,
    u.FullName AS RequesterName,
    u.Username AS RequesterUsername,
    ar.ResourceName,
    ar.Reason,
    ar.RequestedAt,
    ar.Status
FROM dbo.AccessRequests ar
INNER JOIN dbo.Users u ON ar.RequesterUserId = u.UserId
WHERE ar.Status = 'Pending'
ORDER BY ar.RequestedAt ASC;
PRINT '';

PRINT '--- SECTION 4C: FILTER REQUESTS BY SPECIFIC REQUESTER (Employee View: emp.david) ---';

SELECT 
    ar.AccessRequestId,
    ar.ResourceName,
    ar.Reason,
    ar.Status,
    ar.RequestedAt,
    ar.StartAt,
    ar.ExpiresAt,
    approver.FullName AS DecidedBy,
    ar.DecisionAt,
    ar.DecisionComment
FROM dbo.AccessRequests ar
INNER JOIN dbo.Users requester ON ar.RequesterUserId = requester.UserId
LEFT JOIN dbo.Users approver ON ar.ApproverUserId = approver.UserId
WHERE requester.Username = N'emp.david'
ORDER BY ar.RequestedAt DESC;
PRINT '';

-- ============================================================================
-- SECTION 5: Audit Log Compliance & Chronological History
-- ============================================================================
PRINT '--- SECTION 5: AUDIT LOG TIMELINE QUERY (Most recent first) ---';

SELECT TOP 10
    al.AuditLogId,
    al.CreatedAt,
    ISNULL(u.Username, N'[SYSTEM / UNAUTH]') AS Initiator,
    al.Action,
    al.ResourceType,
    al.ResourceId,
    al.IpAddress,
    al.Details
FROM dbo.AuditLogs al
LEFT JOIN dbo.Users u ON al.UserId = u.UserId
ORDER BY al.CreatedAt DESC;
PRINT '';

-- ============================================================================
-- SECTION 6: Negative Constraint Tests (Using Transactions with Rollback)
-- Ensures constraints actively protect data integrity without altering the DB.
-- ============================================================================
PRINT '============================================================================';
PRINT '--- SECTION 6: NEGATIVE CONSTRAINT & INTEGRITY TESTS (Transaction Rollbacks) ---';
PRINT '============================================================================';

-- Test 6.1: Reject Duplicate Username
PRINT 'Running Test 6.1: Duplicate Username Rejection...';
BEGIN TRANSACTION;
BEGIN TRY
    INSERT INTO dbo.Users (FullName, Username, Email, PasswordHash, IsActive)
    VALUES (N'Duplicate Test', N'admin.sarah', N'unique.sarah@test.internal', N'DummyHash', 1);

    PRINT 'FAIL: Duplicate username was unexpectedly accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.1]: Duplicate username rejected as expected. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.2: Reject Duplicate User-Role Assignment
PRINT 'Running Test 6.2: Duplicate User-Role Assignment Rejection...';
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @ExistingUserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'admin.sarah');
    DECLARE @ExistingRoleId INT = (SELECT TOP 1 RoleId FROM dbo.Roles WHERE RoleName = N'Admin');

    INSERT INTO dbo.UserRoles (UserId, RoleId)
    VALUES (@ExistingUserId, @ExistingRoleId);

    PRINT 'FAIL: Duplicate user-role assignment was unexpectedly accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.2]: Duplicate user-role assignment rejected as expected. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.3: Reject Invalid AccessRequest Status
PRINT 'Running Test 6.3: Invalid AccessRequest Status Check Constraint...';
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @ReqUserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'emp.david');

    INSERT INTO dbo.AccessRequests (RequesterUserId, ResourceName, Reason, Status)
    VALUES (@ReqUserId, N'TEST-RESOURCE', N'Valid justification for test', N'InvalidStatusValue');

    PRINT 'FAIL: Invalid status value was unexpectedly accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.3]: Invalid status rejected by CK_AccessRequests_Status. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.4: Reject Inverted Time Window (StartAt > ExpiresAt)
PRINT 'Running Test 6.4: Inverted Time Window Check Constraint...';
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @ReqUser2 INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'emp.david');
    DECLARE @AppUser2 INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'approver.marcus');

    INSERT INTO dbo.AccessRequests 
        (RequesterUserId, ResourceName, Reason, Status, ApproverUserId, DecisionAt, StartAt, ExpiresAt)
    VALUES 
        (@ReqUser2, N'TEST-RESOURCE', N'Valid justification for test', N'Approved', 
         @AppUser2, SYSUTCDATETIME(), SYSUTCDATETIME(), DATEADD(HOUR, -2, SYSUTCDATETIME()));

    PRINT 'FAIL: Inverted time window (StartAt > ExpiresAt) was unexpectedly accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.4]: Inverted time window rejected by CK_AccessRequests_TimeWindow. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.5: Reject Pending Request With Decision Timestamp (Decision Consistency)
PRINT 'Running Test 6.5: Decision Consistency Check Constraint...';
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @ReqUser3 INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'emp.david');
    DECLARE @AppUser3 INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = N'approver.marcus');

    -- Status is 'Pending' but ApproverUserId and DecisionAt are populated
    INSERT INTO dbo.AccessRequests 
        (RequesterUserId, ResourceName, Reason, Status, ApproverUserId, DecisionAt)
    VALUES 
        (@ReqUser3, N'TEST-RESOURCE', N'Valid justification for test', N'Pending', @AppUser3, SYSUTCDATETIME());

    PRINT 'FAIL: Inconsistent decision fields on Pending status were accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.5]: Inconsistent decision fields rejected by CK_AccessRequests_DecisionFields. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.6: Reject Non-Existent Foreign Key Reference
PRINT 'Running Test 6.6: Foreign Key Enforcement (Non-existent RequesterUserId)...';
BEGIN TRANSACTION;
BEGIN TRY
    INSERT INTO dbo.AccessRequests (RequesterUserId, ResourceName, Reason, Status)
    VALUES (999999, N'TEST-RESOURCE', N'Valid justification for test', N'Pending');

    PRINT 'FAIL: AccessRequest with invalid RequesterUserId was accepted!';
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'PASS [Test 6.6]: Foreign key violation rejected as expected. Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    ROLLBACK TRANSACTION;
END CATCH;

-- Test 6.7: Verify AuditLogs Preservation on User Deletion (ON DELETE SET NULL)
PRINT 'Running Test 6.7: Audit Log Preservation via ON DELETE SET NULL...';
BEGIN TRANSACTION;
BEGIN TRY
    -- 1. Insert a temporary user
    INSERT INTO dbo.Users (FullName, Username, Email, PasswordHash, IsActive)
    VALUES (N'Audit Temp User', N'temp.audit.user', N'temp.audit@test.internal', N'DummyHash', 1);
    DECLARE @TempUserId INT = SCOPE_IDENTITY();

    -- 2. Insert an audit log referencing the temp user
    INSERT INTO dbo.AuditLogs (UserId, Action, ResourceType, ResourceId, Details)
    VALUES (@TempUserId, N'TEMP_TEST_ACTION', N'TestResource', N'999', N'Temporary audit record for deletion test.');
    DECLARE @TempAuditLogId INT = SCOPE_IDENTITY();

    -- 3. Delete the temporary user
    DELETE FROM dbo.Users WHERE UserId = @TempUserId;

    -- 4. Check if audit log still exists and UserId was safely set to NULL
    DECLARE @PreservedLogUserId INT;
    DECLARE @LogExists BIT = 0;

    SELECT @LogExists = 1, @PreservedLogUserId = UserId 
    FROM dbo.AuditLogs 
    WHERE AuditLogId = @TempAuditLogId;

    IF @LogExists = 1 AND @PreservedLogUserId IS NULL
        PRINT 'PASS [Test 6.7]: Audit log preserved with UserId set to NULL after user deletion (immutable audit trail preserved).';
    ELSE
        PRINT 'FAIL: Audit log was either deleted or UserId was not set to NULL!';

    -- Roll back the transaction so test records do not persist
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    PRINT 'ERROR during Test 6.7: ' + ERROR_MESSAGE();
    ROLLBACK TRANSACTION;
END CATCH;

PRINT '';
PRINT '============================================================================';
PRINT 'ALL VERIFICATION QUERIES AND TESTS EXECUTED SUCCESSFULLY.';
PRINT '============================================================================';
GO
