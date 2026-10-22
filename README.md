# SecureAccess — Privileged Access Management System

Enterprise Privileged Access Management (PAM) system engineered with C#, Microsoft SQL Server, ASP.NET Core Web API, and C# Windows Forms (WinForms).

---

## Architecture Roadmap & Project Status

- **Phase 1 (Completed):** Microsoft SQL Server Database Architecture ([`Database/`](./Database))
  - 3NF relational schema (7 core tables: `Users`, `Roles`, `UserRoles`, `Permissions`, `RolePermissions`, `AccessRequests`, `AuditLogs`).
  - Read Committed Snapshot Isolation (RCSI), UTC datetime defaults, foreign key cascading safety, and business check constraints.
- **Phase 2 (Completed):** ASP.NET Core Web API Backend (.NET 8) ([`SecureAccess.Api/`](./SecureAccess.Api))
  - Entity Framework Core with SQL Server resilience (`EnableRetryOnFailure`) and in-memory test fallback.
  - PBKDF2 HMAC-SHA256 password hashing (10,000 iterations), signed JWT authentication, and policy-based authorization.
  - Append-only audit logging and centralized error handling middleware.
- **Phase 3 (Completed):** Professional C# Windows Forms Desktop UI ([`SecureAccess.WinForms/`](./SecureAccess.WinForms))
  - High-DPI enabled desktop application with curated dark navy (`#111827`) and slate (`#F8FAFC`) design.
  - Views: Dashboard overview, User management, Roles & Permissions catalog, Access Requests portal, Approvals triage queue, and Audit log inspector.
  - Pure desktop client communicating with the Web API over HTTP REST with Bearer tokens; no direct database connection from UI.
- **Phase 4 (Completed):** Testing, Security Hardening & Finalization ([`SecureAccess.Api.Tests/`](./SecureAccess.Api.Tests))
  - Comprehensive 40-test automated test suite (Unit, Service, Client, and WebApplicationFactory Integration tests).
  - Hardened authorization boundaries: self-approval prevention, self-role modification prevention, self-deactivation lockout guards, anti-duplicate pending request protection.
  - Production secret management validation and configuration templates (`appsettings.Example.json`).

---

## Strict Technology Standards

- **Core Languages:** C# (.NET 8) and Microsoft SQL Server T-SQL only.
- **Strictly Prohibited:** No TypeScript (`.ts`), no TSX (`.tsx`), no React, Next.js, Node.js, npm, or web frontends.
- **Desktop Client:** 100% native C# Windows Forms.
- **Backend:** ASP.NET Core Web API with JWT Bearer authentication.
- **Database:** Microsoft SQL Server schema with Entity Framework Core data access.

---

## Project Structure

```
secureaccess/
├── SecureAccess.sln                    # Visual Studio / .NET 8 Master Solution
├── README.md                           # Main solution documentation & run instructions
│
├── Database/                           # Phase 1: Database Architecture
│   ├── 01_CreateDatabase.sql           # Database & schema creation (3NF, 7 tables)
│   ├── 02_SeedData.sql                 # Idempotent demo identities & workflows
│   ├── 03_IndexesAndConstraints.sql    # Performance indexes & state CHECK constraints
│   ├── 04_VerificationQueries.sql      # Schema checks & negative transactional tests
│   └── README.md                       # Phase 1 documentation & SSMS execution guide
│
├── SecureAccess.Api/                   # Phase 2: ASP.NET Core Web API Backend
│   ├── Program.cs                      # Host configuration, JWT auth, policies, pipeline
│   ├── appsettings.json                # Local development settings & connection strings
│   ├── appsettings.Example.json        # Safe production configuration template
│   ├── Entities/                       # EF Core models matching SQL Server tables
│   ├── Data/                           # SecureAccessDbContext and DatabaseSeeder
│   ├── DTOs/                           # Request/Response contracts (Auth, Users, Requests, Audits)
│   ├── Services/                       # Workflow services, TokenService, PasswordHasher, Auditing
│   ├── Controllers/                    # RESTful endpoints (Auth, Users, Roles, Requests, Audits)
│   ├── Middleware/                     # GlobalExceptionMiddleware (Sanitized error responses)
│   └── README.md                       # Phase 2 API setup, endpoints, & testing guide
│
├── SecureAccess.WinForms/              # Phase 3: Enterprise Desktop UI
│   ├── Program.cs                      # Application entry point & High-DPI setup
│   ├── Services/                       # ApiClient (HTTP REST) and UserSession (In-Memory Auth)
│   ├── Helpers/                        # UiTheme (Dark navy sidebar, Segoe UI fonts, grid styles)
│   ├── Controls/                       # DashboardView, UsersView, RolesView, AccessRequestsView, ApprovalsView, AuditLogsView
│   ├── Forms/                          # LoginForm, MainForm, CreateUserDialog, EditUserDialog, AssignRolesDialog, NewRequestDialog, RequestDetailsDialog, PromptDialog
│   └── README.md                       # Phase 3 desktop UI guide, screens, & run instructions
│
└── SecureAccess.Api.Tests/             # Phase 4: Automated Test Suite (40 Tests)
    ├── AuthTests.cs                    # Password hashing & JWT claim unit tests
    ├── AccessRequestWorkflowTests.cs   # PAM workflow, state transitions, & self-approval prohibition
    ├── UserManagementTests.cs          # User CRUD, role assignment, and self-modification guards
    ├── WinFormsClientTests.cs          # Desktop client session and RBAC logic tests
    └── ApiIntegrationTests.cs          # WebApplicationFactory end-to-end integration tests
```

---

## Security Hardening Features (Phase 4)

1. **Self-Approval Prohibition:**
   - Enforced server-side in `AccessRequestService.ApproveRequestAsync`. If `RequesterUserId == ApproverUserId`, an `InvalidOperationException` is thrown.
   - Visually indicated and disabled in `ApprovalsView.cs` with warning banner.
2. **Self-Role Modification Prevention:**
   - Enforced in `UserService.UpdateUserRolesAsync`. Administrators cannot alter their own assigned roles, preventing rogue privilege self-assignment or accidental lockout.
3. **Self-Deactivation Guard:**
   - Enforced in `UserService.UpdateUserStatusAsync`. Prevents active administrators from disabling their own accounts.
4. **Anti-Duplicate Pending Request Protection:**
   - Enforced in `AccessRequestService.CreateRequestAsync`. Users cannot submit duplicate pending access requests for the same target resource while a previous request is still pending triage.
5. **Window Expiration Boundary Validation:**
   - Access requests cannot be submitted or approved with expiration timestamps in the past.
   - `StartAt <= ExpiresAt` enforced both in API services and database check constraints (`CK_AccessRequests_TimeWindow`).
6. **Route Parameter Validation:**
   - Positive integer validation (`id > 0`) across all controller endpoints (`UsersController`, `AccessRequestsController`).
7. **Safe Exception & Sanitized Error Responses:**
   - `GlobalExceptionMiddleware` catches unhandled exceptions, mapping database/timeout failures to HTTP 503 / 500 without leaking connection strings, server IPs, or stack traces.
8. **Production Secret Enforcement:**
   - In non-development environments, the API checks that `Jwt:Key` is not set to the default development fallback secret, throwing an immediate startup exception if an unconfigured key is detected.

---

## Prerequisites

- **.NET SDK:** Microsoft .NET 8.0 SDK (`dotnet --version` >= 8.0.x)
- **Operating System:** Windows 10/11 or Windows Server (required for WinForms desktop client)
- **Database (Optional for Dev/Test):** Microsoft SQL Server 2017+, SQL Server Express, or Azure SQL Database.
  - *Note:* The API includes an automatic in-memory fallback mode (`"UseInMemoryDatabase": true` or when SQL Server is offline), allowing full application execution and automated test runs even without a running local SQL Server instance.

---

## Configuration & Secret Setup

### Local Development Configuration
Local development settings are in [`SecureAccess.Api/appsettings.json`](./SecureAccess.Api/appsettings.json).

### Production Configuration (Secure Setup)
A production configuration template is provided at [`SecureAccess.Api/appsettings.Example.json`](./SecureAccess.Api/appsettings.Example.json).

For production deployments, pass secrets securely via environment variables or secret managers:
- Connection String: `ConnectionStrings__SecureAccessConnection`
- JWT Secret Key: `Jwt__Key` (minimum 32 bytes cryptographically random string)

```powershell
$env:ConnectionStrings__SecureAccessConnection = "Server=sql.prod.internal;Database=SecureAccessDB;User Id=pam_svc;Password=YourComplexPassword!;TrustServerCertificate=False;Encrypt=True;"
$env:Jwt__Key = "YourSuperSecretCryptographicallyRandomKeyMustBe32BytesLong!"
```

---

## Build, Test & Run Instructions

### 1. Build the Entire Master Solution
Run from the repository root:
```powershell
dotnet build SecureAccess.sln
```
Expected output: **0 Warning(s), 0 Error(s)**.

### 2. Run All Automated Tests
Run the 40 automated tests:
```powershell
dotnet test SecureAccess.sln
```
Expected output: **Total: 40, Passed: 40, Failed: 0, Skipped: 0**.

### 3. Initialize the SQL Server Database (When Using Real SQL Server)
Open SQL Server Management Studio (SSMS) or `sqlcmd`, connect to your instance, and execute the scripts in order:
1. [`Database/01_CreateDatabase.sql`](./Database/01_CreateDatabase.sql)
2. [`Database/02_SeedData.sql`](./Database/02_SeedData.sql)
3. [`Database/03_IndexesAndConstraints.sql`](./Database/03_IndexesAndConstraints.sql)
4. [`Database/04_VerificationQueries.sql`](./Database/04_VerificationQueries.sql)

### 4. Start the ASP.NET Core Web API Backend
```powershell
cd SecureAccess.Api
dotnet run
```
- API Base URL: `http://localhost:5154`
- OpenAPI Swagger Documentation: `http://localhost:5154/swagger`

### 5. Launch the WinForms Desktop UI Client
In a separate terminal window:
```powershell
cd SecureAccess.WinForms
dotnet run
```

---

## Seeded Demo User Accounts

All demo accounts use password: **`Password123!`**

| Username | Role | Primary Permissions & Responsibilities |
| :--- | :--- | :--- |
| `admin.sarah` | **Admin** | Full system governance, identity provisioning, role assignments, audit log review. |
| `approver.marcus` | **Approver** | Triage queue review, access request approval & rejection, session revocation. |
| `approver.elena` | **Approver** | Secondary approver for access triage and compliance reviews. |
| `emp.david` | **Employee** | Submits privileged access requests, inspects personal request statuses. |
| `emp.rachel` | **Employee** | Standard user identity for workflow testing. |

---

## Important Architectural Notes

> [!NOTE]
> **Privileged Access Management (PAM) Scope:**
> SecureAccess is an authorization and credential grant workflow management system. Approval and revocation decisions establish time-bounded valid grants within the PAM database. Technical enforcement on external target infrastructure (e.g. AWS IAM, active SSH tunnels, or remote SQL instances) requires dedicated host agent integrations and is not falsely simulated or claimed.

> [!TIP]
> **Zero Direct Database Calls from Desktop Client:**
> The WinForms client never connects to SQL Server directly. All data access occurs through the ASP.NET Core Web API using authenticated JWT Bearer headers, enforcing strict boundary security and network isolation.
