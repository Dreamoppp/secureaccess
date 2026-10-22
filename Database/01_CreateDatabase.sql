-- ============================================================================
-- SecureAccess - Privileged Access Management System
-- Script: 01_CreateDatabase.sql
-- Description: Creates the SecureAccessDB database and core normalized tables
--              with primary keys, foreign keys, unique constraints, and defaults.
-- Target RDBMS: Microsoft SQL Server 2017+ / Azure SQL Database
-- Execution Order: 1 of 4
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Database Creation
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SecureAccessDB')
BEGIN
    PRINT 'Creating database SecureAccessDB...';
    CREATE DATABASE [SecureAccessDB]
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
ELSE
BEGIN
    PRINT 'Database SecureAccessDB already exists.';
END
GO

USE [SecureAccessDB];
GO

-- Enable Read Committed Snapshot Isolation (RCSI) for high-concurrency API/desktop usage
-- (Avoids blocking read operations while writes occur)
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'SecureAccessDB') = 0
BEGIN
    ALTER DATABASE [SecureAccessDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK AFTER 5 SECONDS;
END
GO

-- ----------------------------------------------------------------------------
-- 2. Table: Users
-- Stores user accounts, identity details, and authentication metadata.
-- Passwords are stored exclusively as secure cryptographic hashes (e.g. PBKDF2/Argon2).
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Users' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.Users...';
    CREATE TABLE dbo.Users
    (
        UserId          INT IDENTITY(1,1)   NOT NULL,
        FullName        NVARCHAR(150)       NOT NULL,
        Username        NVARCHAR(50)        NOT NULL,
        Email           NVARCHAR(256)       NOT NULL,
        PasswordHash    NVARCHAR(256)       NOT NULL,
        IsActive        BIT                 NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt       DATETIME2(7)        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(7)        NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (SYSUTCDATETIME()),

        -- Constraints
        CONSTRAINT PK_Users_UserId PRIMARY KEY CLUSTERED (UserId),
        CONSTRAINT UQ_Users_Username UNIQUE NONCLUSTERED (Username),
        CONSTRAINT UQ_Users_Email UNIQUE NONCLUSTERED (Email),
        CONSTRAINT CK_Users_Username_NotEmpty CHECK (LEN(TRIM(Username)) >= 3),
        CONSTRAINT CK_Users_Email_Format CHECK (Email LIKE '%_@__%.__%')
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.Users already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 3. Table: Roles
-- Defines system roles for Role-Based Access Control (RBAC).
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Roles' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.Roles...';
    CREATE TABLE dbo.Roles
    (
        RoleId          INT IDENTITY(1,1)   NOT NULL,
        RoleName        NVARCHAR(50)        NOT NULL,
        Description     NVARCHAR(250)       NULL,
        CreatedAt       DATETIME2(7)        NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),

        -- Constraints
        CONSTRAINT PK_Roles_RoleId PRIMARY KEY CLUSTERED (RoleId),
        CONSTRAINT UQ_Roles_RoleName UNIQUE NONCLUSTERED (RoleName),
        CONSTRAINT CK_Roles_RoleName_NotEmpty CHECK (LEN(TRIM(RoleName)) >= 2)
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.Roles already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 4. Table: UserRoles
-- Associative entity establishing the Many-to-Many relationship between
-- Users and Roles. A user can possess multiple roles simultaneously.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'UserRoles' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.UserRoles...';
    CREATE TABLE dbo.UserRoles
    (
        UserId          INT                 NOT NULL,
        RoleId          INT                 NOT NULL,
        AssignedAt      DATETIME2(7)        NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),

        -- Composite Primary Key ensures a user cannot be assigned the same role more than once
        CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId) ON DELETE CASCADE
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.UserRoles already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 5. Table: Permissions
-- Defines discrete granular operations on resources within the PAM system.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Permissions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.Permissions...';
    CREATE TABLE dbo.Permissions
    (
        PermissionId    INT IDENTITY(1,1)   NOT NULL,
        PermissionName  NVARCHAR(100)       NOT NULL,
        Resource        NVARCHAR(100)       NOT NULL,
        Description     NVARCHAR(250)       NULL,
        CreatedAt       DATETIME2(7)        NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),

        -- Constraints
        CONSTRAINT PK_Permissions_PermissionId PRIMARY KEY CLUSTERED (PermissionId),
        CONSTRAINT UQ_Permissions_PermissionName UNIQUE NONCLUSTERED (PermissionName),
        CONSTRAINT CK_Permissions_PermissionName_NotEmpty CHECK (LEN(TRIM(PermissionName)) >= 3)
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.Permissions already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 6. Table: RolePermissions
-- Architectural Rationale:
-- In a normalized Role-Based Access Control (RBAC) architecture, Permissions
-- exist as atomic operations, while Roles represent functional authorization
-- bundles. Because a Role contains multiple Permissions and a Permission can
-- be shared by multiple Roles (Many-to-Many), this junction table is mandatory.
-- Without RolePermissions, permissions cannot be bound to roles and the RBAC
-- model cannot be enforced at runtime.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'RolePermissions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.RolePermissions (RBAC Role-Permission Mapping)...';
    CREATE TABLE dbo.RolePermissions
    (
        RoleId          INT                 NOT NULL,
        PermissionId    INT                 NOT NULL,
        AssignedAt      DATETIME2(7)        NOT NULL CONSTRAINT DF_RolePermissions_AssignedAt DEFAULT (SYSUTCDATETIME()),

        -- Composite Primary Key prevents duplicate permission assignments to a single role
        CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId),
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId) ON DELETE CASCADE,
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (PermissionId) ON DELETE CASCADE
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.RolePermissions already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 7. Table: AccessRequests
-- Manages time-bounded privileged access workflows requested by employees
-- and adjudicated by authorized approvers.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AccessRequests' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.AccessRequests...';
    CREATE TABLE dbo.AccessRequests
    (
        AccessRequestId INT IDENTITY(1,1)   NOT NULL,
        RequesterUserId INT                 NOT NULL,
        ResourceName    NVARCHAR(100)       NOT NULL,
        Reason          NVARCHAR(500)       NOT NULL,
        RequestedAt     DATETIME2(7)        NOT NULL CONSTRAINT DF_AccessRequests_RequestedAt DEFAULT (SYSUTCDATETIME()),
        StartAt         DATETIME2(7)        NULL,
        ExpiresAt       DATETIME2(7)        NULL,
        Status          NVARCHAR(20)        NOT NULL CONSTRAINT DF_AccessRequests_Status DEFAULT ('Pending'),
        ApproverUserId  INT                 NULL,
        DecisionAt      DATETIME2(7)        NULL,
        DecisionComment NVARCHAR(500)       NULL,

        -- Constraints
        CONSTRAINT PK_AccessRequests_AccessRequestId PRIMARY KEY CLUSTERED (AccessRequestId),
        CONSTRAINT FK_AccessRequests_Requester FOREIGN KEY (RequesterUserId) REFERENCES dbo.Users (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_AccessRequests_Approver FOREIGN KEY (ApproverUserId) REFERENCES dbo.Users (UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_AccessRequests_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected', 'Revoked')),
        CONSTRAINT CK_AccessRequests_Reason_NotEmpty CHECK (LEN(TRIM(Reason)) >= 5)
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.AccessRequests already exists.';
END
GO

-- ----------------------------------------------------------------------------
-- 8. Table: AuditLogs
-- Immutable, append-only record of security events, administrative actions,
-- access reviews, and session logins.
-- Deletion policy: Foreign key to Users uses ON DELETE SET NULL to preserve
-- historical audit trails even if a user account is removed or anonymized.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AuditLogs' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Creating table dbo.AuditLogs...';
    CREATE TABLE dbo.AuditLogs
    (
        AuditLogId      INT IDENTITY(1,1)   NOT NULL,
        UserId          INT                 NULL,
        Action          NVARCHAR(100)       NOT NULL,
        ResourceType    NVARCHAR(100)       NOT NULL,
        ResourceId      NVARCHAR(100)       NULL,
        Details         NVARCHAR(MAX)       NOT NULL,
        IpAddress       NVARCHAR(45)        NULL, -- Supports both IPv4 and IPv6 string lengths
        CreatedAt       DATETIME2(7)        NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),

        -- Constraints
        CONSTRAINT PK_AuditLogs_AuditLogId PRIMARY KEY CLUSTERED (AuditLogId),
        CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE SET NULL,
        CONSTRAINT CK_AuditLogs_Action_NotEmpty CHECK (LEN(TRIM(Action)) >= 2)
    );
END
ELSE
BEGIN
    PRINT 'Table dbo.AuditLogs already exists.';
END
GO

PRINT 'Database and core tables created successfully.';
GO
