-- ============================================================================
-- SecureAccess - Privileged Access Management System
-- Script: 03_IndexesAndConstraints.sql
-- Description: Creates additional performance indexes (foreign keys, filtered,
--              composite) and advanced business logic check constraints.
--              Fully idempotent and safe to rerun without errors.
-- Target RDBMS: Microsoft SQL Server 2017+ / Azure SQL Database
-- Execution Order: 3 of 4
-- ============================================================================

USE [SecureAccessDB];
GO

SET NOCOUNT ON;
PRINT 'Deploying advanced indexes and business constraints for SecureAccessDB...';

-- ============================================================================
-- 1. Performance Indexes on Foreign Keys and Associative Entities
-- Note: SQL Server does not automatically index foreign keys. Indexing them
-- prevents full table scans on parent-child joins and avoids locking contention.
-- ============================================================================

-- Index to support finding all users assigned to a specific role
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRoles_RoleId' AND object_id = OBJECT_ID('dbo.UserRoles'))
BEGIN
    PRINT 'Creating index IX_UserRoles_RoleId on dbo.UserRoles...';
    CREATE NONCLUSTERED INDEX IX_UserRoles_RoleId
        ON dbo.UserRoles (RoleId)
        INCLUDE (AssignedAt);
END
GO

-- Index to support finding all roles that include a specific permission
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RolePermissions_PermissionId' AND object_id = OBJECT_ID('dbo.RolePermissions'))
BEGIN
    PRINT 'Creating index IX_RolePermissions_PermissionId on dbo.RolePermissions...';
    CREATE NONCLUSTERED INDEX IX_RolePermissions_PermissionId
        ON dbo.RolePermissions (PermissionId)
        INCLUDE (AssignedAt);
END
GO

-- Index to accelerate querying active user directory accounts
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_IsActive_Filtered' AND object_id = OBJECT_ID('dbo.Users'))
BEGIN
    PRINT 'Creating filtered index IX_Users_IsActive_Filtered on dbo.Users...';
    CREATE NONCLUSTERED INDEX IX_Users_IsActive_Filtered
        ON dbo.Users (IsActive)
        INCLUDE (UserId, Username, FullName, Email)
        WHERE IsActive = 1;
END
GO

-- ============================================================================
-- 2. Performance Indexes for AccessRequests
-- Optimized for high-frequency queries in the WinForms UI & REST API:
-- - Filtering by Requester (Employee portal)
-- - Filtering by Status & Date (Approver triage queue)
-- - Filtered index for pending requests (Sub-millisecond triage queries)
-- ============================================================================

-- Fast lookup by RequesterUserId (Employee dashboard: "My Access Requests")
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequests_RequesterUserId' AND object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Creating index IX_AccessRequests_RequesterUserId on dbo.AccessRequests...';
    CREATE NONCLUSTERED INDEX IX_AccessRequests_RequesterUserId
        ON dbo.AccessRequests (RequesterUserId, RequestedAt DESC)
        INCLUDE (ResourceName, Status, StartAt, ExpiresAt);
END
GO

-- Fast lookup by ApproverUserId (Approver decision history & audit review)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequests_ApproverUserId' AND object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Creating index IX_AccessRequests_ApproverUserId on dbo.AccessRequests...';
    CREATE NONCLUSTERED INDEX IX_AccessRequests_ApproverUserId
        ON dbo.AccessRequests (ApproverUserId, DecisionAt DESC)
        WHERE ApproverUserId IS NOT NULL;
END
GO

-- Composite index for status filtering and queue sorting
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequests_Status_RequestedAt' AND object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Creating index IX_AccessRequests_Status_RequestedAt on dbo.AccessRequests...';
    CREATE NONCLUSTERED INDEX IX_AccessRequests_Status_RequestedAt
        ON dbo.AccessRequests (Status, RequestedAt DESC)
        INCLUDE (AccessRequestId, RequesterUserId, ResourceName, ApproverUserId, DecisionAt);
END
GO

-- Filtered index dedicated to the Approver Pending Queue (highest frequency UI refresh)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequests_Pending_Queue' AND object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Creating filtered index IX_AccessRequests_Pending_Queue on dbo.AccessRequests...';
    CREATE NONCLUSTERED INDEX IX_AccessRequests_Pending_Queue
        ON dbo.AccessRequests (RequestedAt ASC)
        INCLUDE (AccessRequestId, RequesterUserId, ResourceName, Reason)
        WHERE Status = 'Pending';
END
GO

-- Index to support background revocation/expiration monitoring workers
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequests_ActiveApproved_Window' AND object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Creating index IX_AccessRequests_ActiveApproved_Window on dbo.AccessRequests...';
    CREATE NONCLUSTERED INDEX IX_AccessRequests_ActiveApproved_Window
        ON dbo.AccessRequests (ExpiresAt, Status)
        INCLUDE (AccessRequestId, RequesterUserId, ResourceName, StartAt)
        WHERE Status = 'Approved';
END
GO

-- ============================================================================
-- 3. Performance Indexes for AuditLogs
-- Supports compliance reporting, security investigation, and timeline queries.
-- ============================================================================

-- Primary timeline query index (Chronological audit history viewing)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_CreatedAt' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    PRINT 'Creating index IX_AuditLogs_CreatedAt on dbo.AuditLogs...';
    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAt
        ON dbo.AuditLogs (CreatedAt DESC)
        INCLUDE (AuditLogId, UserId, Action, ResourceType, ResourceId, IpAddress);
END
GO

-- User activity history index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_UserId_CreatedAt' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    PRINT 'Creating index IX_AuditLogs_UserId_CreatedAt on dbo.AuditLogs...';
    CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId_CreatedAt
        ON dbo.AuditLogs (UserId, CreatedAt DESC)
        INCLUDE (Action, ResourceType, ResourceId)
        WHERE UserId IS NOT NULL;
END
GO

-- Action filtering index (e.g. filter by USER_LOGIN_FAILED or ACCESS_REQUEST_REVOKED)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Action_CreatedAt' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    PRINT 'Creating index IX_AuditLogs_Action_CreatedAt on dbo.AuditLogs...';
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Action_CreatedAt
        ON dbo.AuditLogs (Action, CreatedAt DESC)
        INCLUDE (UserId, ResourceType, ResourceId, Details);
END
GO

-- Resource correlation index (Track all security actions performed on a given resource)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_ResourceType_ResourceId' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    PRINT 'Creating index IX_AuditLogs_ResourceType_ResourceId on dbo.AuditLogs...';
    CREATE NONCLUSTERED INDEX IX_AuditLogs_ResourceType_ResourceId
        ON dbo.AuditLogs (ResourceType, ResourceId, CreatedAt DESC)
        INCLUDE (UserId, Action, IpAddress);
END
GO

-- ============================================================================
-- 4. Advanced Business Logic CHECK Constraints
-- ============================================================================

-- Constraint 1: Time Window Integrity
-- Ensures that if both StartAt and ExpiresAt are provided, StartAt <= ExpiresAt.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AccessRequests_TimeWindow' AND parent_object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Adding constraint CK_AccessRequests_TimeWindow to dbo.AccessRequests...';
    ALTER TABLE dbo.AccessRequests
    ADD CONSTRAINT CK_AccessRequests_TimeWindow
    CHECK (StartAt IS NULL OR ExpiresAt IS NULL OR StartAt <= ExpiresAt);
END
ELSE
BEGIN
    PRINT 'Constraint CK_AccessRequests_TimeWindow already exists.';
END
GO

-- Constraint 2: Decision Field Consistency
-- Enforces strict state integrity:
-- - A Pending request MUST NOT have an Approver or Decision timestamp.
-- - An Approved, Rejected, or Revoked request MUST have both an Approver and Decision timestamp.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AccessRequests_DecisionFields' AND parent_object_id = OBJECT_ID('dbo.AccessRequests'))
BEGIN
    PRINT 'Adding constraint CK_AccessRequests_DecisionFields to dbo.AccessRequests...';
    ALTER TABLE dbo.AccessRequests
    ADD CONSTRAINT CK_AccessRequests_DecisionFields
    CHECK (
        (Status = 'Pending' AND ApproverUserId IS NULL AND DecisionAt IS NULL)
        OR
        (Status IN ('Approved', 'Rejected', 'Revoked') AND ApproverUserId IS NOT NULL AND DecisionAt IS NOT NULL)
    );
END
ELSE
BEGIN
    PRINT 'Constraint CK_AccessRequests_DecisionFields already exists.';
END
GO

PRINT 'Additional indexes and business constraints deployed successfully.';
GO
