# SecureAccess — Database Architecture & Setup Guide (Phase 1)

## 1. System Overview

**SecureAccess** is an enterprise-grade Privileged Access Management (PAM) system engineered to regulate, monitor, and audit elevated permissions across corporate infrastructure.

### Technology Stack & Architecture Roadmap
- **Database (Phase 1 — Current):** Microsoft SQL Server 2017+ / Azure SQL Database
- **Backend (Future Phase 2):** C# and ASP.NET Core Web API (.NET 8+)
- **Desktop UI (Future Phase 3):** C# Windows Forms (.NET 8+ WinForms)
- **Strict Guidelines:** No web frontends, no Node.js/npm dependencies, and no TypeScript/TSX. The planned user interface is a Windows desktop application.

---

## 2. Relational Database Design

The `SecureAccessDB` schema is fully normalized (Third Normal Form / 3NF) and consists of 7 tables designed for integrity, performance, and compliance.

```
 +--------------------+       +------------------------+       +--------------------+
 |     dbo.Users      | 1   * |     dbo.UserRoles      | *   1 |     dbo.Roles      |
 +--------------------+-------+------------------------+-------+--------------------+
 | PK  UserId         |       | PK,FK1  UserId         |       | PK  RoleId         |
 |     Username (UQ)  |       | PK,FK2  RoleId         |       |     RoleName (UQ)  |
 |     Email (UQ)     |       |         AssignedAt     |       |     Description    |
 |     PasswordHash   |       +------------------------+       |     CreatedAt      |
 |     FullName       |                                        +--------------------+
 |     IsActive       |                                                  | 1
 |     CreatedAt      |                                                  |
 |     UpdatedAt      |                                                  | *
 +--------------------+                                        +--------------------+
   | 1             | 1                                         |dbo.RolePermissions |
   |               |                                           +--------------------+
   | * (Requester) | * (Approver)                              | PK,FK1 RoleId      |
   |               |                                           | PK,FK2 PermissionId|
   |               +---------------+                           |        AssignedAt  |
   |                               |                           +--------------------+
   v                               v                                     | *
 +-----------------------------------+                                   |
 |        dbo.AccessRequests         |                                   | 1
 +-----------------------------------+                                   v
 | PK  AccessRequestId               |                         +--------------------+
 | FK  RequesterUserId               |                         |  dbo.Permissions   |
 |     ResourceName                  |                         +--------------------+
 |     Reason                        |                         | PK  PermissionId   |
 |     RequestedAt                   |                         |     PermissionName |
 |     StartAt (NULL)                |                         |     Resource       |
 |     ExpiresAt (NULL)              |                         |     Description    |
 |     Status (Pending/Approved/...) |                         |     CreatedAt      |
 | FK  ApproverUserId (NULL)         |                         +--------------------+
 |     DecisionAt (NULL)             |
 |     DecisionComment (NULL)        |
 +-----------------------------------+
   | 1
   | (ON DELETE SET NULL)
   v
 +--------------------+
 |   dbo.AuditLogs    |
 +--------------------+
 | PK  AuditLogId     |
 | FK  UserId (NULL)  |
 |     Action         |
 |     ResourceType   |
 |     ResourceId     |
 |     Details        |
 |     IpAddress      |
 |     CreatedAt      |
 +--------------------+
```

### Table Specifications

| Table Name | Primary Key | Description & Integrity Rules |
| :--- | :--- | :--- |
| **`dbo.Users`** | `UserId` (INT IDENTITY) | Stores user profiles. Enforces unique usernames, unique emails with format regex check, active state flag, and PBKDF2/HMAC-SHA256 password hashes. |
| **`dbo.Roles`** | `RoleId` (INT IDENTITY) | Catalog of PAM roles: `Admin`, `Approver`, and `Employee`. Unique role names. |
| **`dbo.UserRoles`** | Composite `(UserId, RoleId)` | Associative table supporting Many-to-Many assignments. Composite PK prevents duplicate assignments. `ON DELETE CASCADE` ensures clean teardown. |
| **`dbo.Permissions`** | `PermissionId` (INT IDENTITY) | Granular securable actions (e.g., `AccessRequests.Approve`, `AuditLogs.View`). Unique permission names. |
| **`dbo.RolePermissions`** | Composite `(RoleId, PermissionId)` | **Mandatory RBAC mapping table.** Binds granular permissions to roles. |
| **`dbo.AccessRequests`** | `AccessRequestId` (INT IDENTITY) | Just-in-Time privileged access requests. Foreign keys to `Users` for Requester and Approver. `CHECK` constraints enforce valid statuses and state consistency. |
| **`dbo.AuditLogs`** | `AuditLogId` (INT IDENTITY) | Append-only security audit trail. Foreign key to `Users` uses `ON DELETE SET NULL` to preserve historical integrity even if an identity is removed. |

---

## 3. Key Architectural Decisions

### 3.1 Rationale for `RolePermissions` Table
In NIST Role-Based Access Control (RBAC), **Permissions** represent atomic operations (e.g. `AccessRequests.Approve`), while **Roles** represent functional responsibility bundles (e.g. `Approver`). 
Because:
1. A single Role comprises multiple Permissions, and
2. A single Permission is shared across multiple Roles (e.g., `Users.Read` belongs to both `Admin` and `Approver`),

the relationship between Roles and Permissions is strictly **Many-to-Many**. Without the `RolePermissions` associative table, permissions would remain disconnected catalog entries, preventing the runtime engine (API/desktop UI) from resolving authorized privileges.

### 3.2 Audit Log Immutability & Preservation
Audit logging in a PAM system must withstand administrative deletions and compliance scrubbing:
- **Append-Only:** Application code will only issue `INSERT` and `SELECT` commands against `dbo.AuditLogs`.
- **Soft Referencing (`ON DELETE SET NULL`):** If a user is deactivated or purged, their past actions in `dbo.AuditLogs` remain intact with `UserId` set to `NULL`, retaining `IpAddress`, `Action`, `Details`, and timestamps for forensics.

### 3.3 High-Precision UTC Timestamps
All date/time fields use `DATETIME2(7)` with default values of `SYSUTCDATETIME()`. This guarantees:
- Microsecond precision.
- Elimination of daylight-saving and time zone ambiguities across distributed clients.

### 3.4 Read Committed Snapshot Isolation (RCSI)
Script `01_CreateDatabase.sql` enables `READ_COMMITTED_SNAPSHOT ON`. This ensures read queries (such as WinForms dashboard refreshes) never block write transactions (such as request approvals), preventing deadlocks.

---

## 4. File Manifest and Execution Order

All Phase 1 scripts reside in the `Database/` directory. They must be executed in sequential numerical order:

```
Database/
│
├── 01_CreateDatabase.sql         [Order 1] Creates database, collation, tables, PKs, FKs, UQs, and defaults.
├── 02_SeedData.sql               [Order 2] Inserts realistic, idempotent demo data for users, roles, and requests.
├── 03_IndexesAndConstraints.sql  [Order 3] Creates foreign key indexes, filtered indexes, and business CHECK constraints.
├── 04_VerificationQueries.sql    [Order 4] Executes schema verification, RBAC matrices, and transactional negative tests.
└── README.md                     [Documentation] This comprehensive guide.
```

---

## 5. Step-by-Step Execution Instructions

### Option A: Using SQL Server Management Studio (SSMS) (Recommended)
1. Open **SQL Server Management Studio**.
2. Connect to your SQL Server instance (e.g., `localhost`, `.\SQLEXPRESS`, or `(localdb)\MSSQLLocalDB`).
3. In SSMS, click **File -> Open -> File...** (or press `Ctrl + O`).
4. Execute the scripts in exact order:
   - Select `01_CreateDatabase.sql` -> Press **F5** (or click **Execute**). Verify "Command(s) completed successfully."
   - Select `02_SeedData.sql` -> Press **F5**. Verify seed counts.
   - Select `03_IndexesAndConstraints.sql` -> Press **F5**. Verify indexes created.
   - Select `04_VerificationQueries.sql` -> Press **F5**. Review the Results and Messages tabs.

### Option B: Using Azure Data Studio or VS Code (MSSQL Extension)
1. Open the project folder `secureaccess` in Azure Data Studio or VS Code.
2. Connect to your SQL Server target connection.
3. Open each file from `01` to `04` and run them sequentially using `Ctrl + Shift + E` (Run Query).

### Option C: Using `sqlcmd` Command-Line Utility
Open PowerShell in the `secureaccess/Database` directory and run:

```powershell
# Using Windows Integrated Authentication (-E) against local default instance
sqlcmd -S localhost -E -i .\01_CreateDatabase.sql
sqlcmd -S localhost -E -i .\02_SeedData.sql
sqlcmd -S localhost -E -i .\03_IndexesAndConstraints.sql
sqlcmd -S localhost -E -i .\04_VerificationQueries.sql
```

*For a named instance such as SQL Express, replace `localhost` with `.\SQLEXPRESS`.*

### Option D: Running via Docker (for Containerized Development)
If you prefer running a local containerized SQL Server:

```powershell
# 1. Start SQL Server 2022 container
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Password123" -p 1433:1433 --name secureaccess-mssql -d mcr.microsoft.com/mssql/server:2022-latest

# 2. Run the deployment scripts via container sqlcmd
docker exec -i secureaccess-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C < .\01_CreateDatabase.sql
docker exec -i secureaccess-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C < .\02_SeedData.sql
docker exec -i secureaccess-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C < .\03_IndexesAndConstraints.sql
docker exec -i secureaccess-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C < .\04_VerificationQueries.sql
```

---

## 6. Seed Data Summary

The seed data in `02_SeedData.sql` is idempotent (safe to run multiple times):

- **Users (8 accounts):**
  - **1 Administrator:** `admin.sarah` (Sarah Jenkins - PAM Administrator)
  - **2 Approvers:** `approver.marcus` (Marcus Vance - Lead Security Officer), `approver.elena` (Elena Rostova - Infrastructure Approver)
  - **5 Employees:** `emp.david`, `emp.rachel`, `emp.carlos`, `emp.priya`, `emp.kevin`
- **Roles (3 roles):** `Admin`, `Approver`, `Employee`.
- **Permissions (14 permissions):** Covering user management, role assignments, request creation, approval, rejection, revocation, and audit inspection.
- **Role Permissions:**
  - `Admin`: Mapped to all 14 permissions.
  - `Approver`: Mapped to 9 permissions (request review, approval, rejection, revocation, audit viewing, directory read).
  - `Employee`: Mapped to 2 permissions (`AccessRequests.Create`, `AccessRequests.ViewSelf`).
- **Access Requests (12 records):**
  - `3 Pending`: Live requests awaiting approver triage.
  - `4 Approved`: Active/completed requests with defined start/expiry windows and approver comments.
  - `3 Rejected`: Denied requests with recorded business justifications.
  - `2 Revoked`: Early-terminated privileged sessions.
- **Audit Logs (14 events):** Telemetry for logins, request lifecycles, and configuration events.

---

## 7. Verification and Testing

Executing `04_VerificationQueries.sql` performs automated checks:

1. **Schema Check:** Verifies all 7 tables exist.
2. **Record Count Check:** Verifies that row volumes match expectations.
3. **Effective Permissions Check:** Resolves the complete User -> Roles -> Permissions matrix.
4. **Queue Views:** Verifies pending triage queues and employee request history.
5. **Negative Tests (With Automatic Rollback):**
   - Duplicate username insertion -> **Rejected** (Error 2627/2601)
   - Duplicate user-role assignment -> **Rejected** (Error 2627)
   - Invalid request status (`'InvalidStatusValue'`) -> **Rejected** (`CK_AccessRequests_Status`)
   - Inverted time window (`StartAt > ExpiresAt`) -> **Rejected** (`CK_AccessRequests_TimeWindow`)
   - Inconsistent decision state (Pending status with decision date) -> **Rejected** (`CK_AccessRequests_DecisionFields`)
   - Non-existent foreign key -> **Rejected** (Error 547)
   - User deletion -> **Audit log preserved** with `UserId = NULL` (`ON DELETE SET NULL`)

---

## 8. Troubleshooting

| Symptom | Cause | Resolution |
| :--- | :--- | :--- |
| `Cannot connect to server` | SQL Server service is stopped or instance name is wrong. | Check Windows Services (`services.msc`) for `SQL Server (MSSQLSERVER)` or `SQL Server (SQLEXPRESS)` and start the service. |
| `Cannot drop the database 'SecureAccessDB' because it is currently in use` | An open query tab is using the database. | Run `ALTER DATABASE [SecureAccessDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;` before dropping. |
| `String or binary data would be truncated` | Value exceeds column length. | Verify input string sizes against table specifications (e.g., `Username` max 50 chars). |
| `The certificate chain was issued by an authority that is not trusted` | Encryption handshake requirement in modern `sqlcmd` or SSMS 20+. | Add `-C` (Trust Server Certificate) parameter to `sqlcmd`, or check "Trust Server Certificate" in SSMS connection properties. |

---

## 9. Future Integration (Phase 2 & Phase 3)

When Phase 2 begins, the connection string in the ASP.NET Core `appsettings.json` will target this database:

```json
{
  "ConnectionStrings": {
    "SecureAccessConnection": "Server=localhost;Database=SecureAccessDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```
