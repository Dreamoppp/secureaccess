# SecureAccess — ASP.NET Core Web API Backend (Phase 2)

## 1. System Overview

**SecureAccess Web API** is the core backend service for the SecureAccess Privileged Access Management (PAM) system. Engineered with **C#** and **ASP.NET Core on .NET 8**, it integrates with Microsoft SQL Server to enforce Role-Based Access Control (RBAC), issue signed JSON Web Tokens (JWT), adjudicate privileged access requests, and log immutable security audit trails.

This API is architected to power both RESTful clients and the planned **C# Windows Forms (WinForms)** desktop client in Phase 3.

---

## 2. Technology Stack & Prerequisites

- **Language / Runtime:** C# 12 / .NET 8.0 SDK (Version 8.0.426 or later)
- **Framework:** ASP.NET Core Web API
- **Data Access:** Entity Framework Core 8.0 (`Microsoft.EntityFrameworkCore.SqlServer`)
- **Authentication:** JWT Bearer Authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **API Documentation:** Swagger / OpenAPI (`Swashbuckle.AspNetCore`)
- **Database:** Microsoft SQL Server 2017+ / Azure SQL Database
- **Testing:** xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`

---

## 3. Architecture & Entity Framework Core Integration

The API mirrors the 3NF relational schema created in Phase 1:

| Entity / DbContext Table | Description |
| :--- | :--- |
| **`Users`** | Identity accounts, email, active flag, and PBKDF2/HMAC-SHA256 password hashes. |
| **`Roles`** | Security roles: `Admin`, `Approver`, `Employee`. |
| **`UserRoles`** | Associative Many-to-Many mapping table with composite PK `(UserId, RoleId)`. |
| **`Permissions`** | Granular securable operations across users, roles, requests, and audits. |
| **`RolePermissions`** | Associative Many-to-Many mapping table binding permissions to roles. |
| **`AccessRequests`** | Just-in-Time privileged access requests with lifecycle states: `Pending`, `Approved`, `Rejected`, `Revoked`. |
| **`AuditLogs`** | Immutable append-only audit records with `ON DELETE SET NULL` user preservation. |

---

## 4. Secure Configuration

The API reads settings from `appsettings.json`, with production overrides supported via environment variables:

```json
{
  "ConnectionStrings": {
    "SecureAccessConnection": "Server=localhost;Database=SecureAccessDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "SecureAccessEnterprisePAMSystemDevSecretKey2026!#$@MustBe32BytesLongMinimum",
    "Issuer": "SecureAccessApi",
    "Audience": "SecureAccessClients",
    "ExpiresInMinutes": 120
  }
}
```

### Environment Variable Overrides for Production:
- `ConnectionStrings__SecureAccessConnection` — SQL Server connection string.
- `Jwt__Key` — 256-bit cryptographically secure signing secret.
- `USE_INMEMORY_DB` — Set to `true` to run against EF Core InMemory for automated testing when SQL Server is offline.

---

## 5. Build, Run, and Swagger

### Build the Solution:
```powershell
dotnet build SecureAccess.sln
```

### Run the Web API:
```powershell
cd SecureAccess.Api
dotnet run
```

### Access Swagger UI:
When the API is running in Development mode, navigate to:
```
https://localhost:7154/swagger
http://localhost:5154/swagger
```

---

## 6. Authentication & Authorization

### 6.1 Authentication Flow
1. Client sends credentials to `POST /api/auth/login`.
2. Password is verified using PBKDF2 HMAC-SHA256 (`IPasswordHasherService`).
3. If verified and user is active, the server issues a signed JWT access token containing:
   - `sub` / `NameIdentifier`: `UserId`
   - `name`: `Username`
   - `role`: Assigned roles (`Admin`, `Approver`, `Employee`)
   - `permission`: Distinct granular permissions derived from `RolePermissions`
4. Client includes token in subsequent requests:
   ```
   Authorization: Bearer <token>
   ```

### 6.2 Pre-seeded Demo Credentials (from Phase 1)
All demo accounts use password: `Password123!`

| Role | Username | Full Name | Primary Capabilities |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin.sarah` | Sarah Jenkins | Full PAM administration, user provisioning, role assignments, audit log review. |
| **Approver** | `approver.marcus` | Marcus Vance | Triage all access requests, approve/reject/revoke grants, audit log review. |
| **Approver** | `approver.elena` | Elena Rostova | Infrastructure approver, access review and decisions. |
| **Employee** | `emp.david` | David Chen | Submit access requests, inspect personal requests. |
| **Employee** | `emp.rachel` | Rachel Adams | Submit access requests, inspect personal requests. |

---

## 7. API Endpoints Catalog

### Authentication
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Anonymous | Authenticates credentials and returns JWT access token. |
| `GET` | `/api/auth/me` | Authenticated | Retrieves current authenticated user profile, roles, and permissions. |

### Users
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/users` | Policy: `Users.Read` | Paginated directory of enterprise users with search and active status filters. |
| `GET` | `/api/users/{id}` | Policy: `Users.Read` | Retrieves detailed user profile by ID. |
| `POST` | `/api/users` | Policy: `Users.Create` | Provisions a new user account with initial role assignments. |
| `PUT` | `/api/users/{id}` | Policy: `Users.Update` | Updates user profile information (FullName, Email). |
| `PATCH`| `/api/users/{id}/status` | Policy: `Users.Deactivate` | Activates or deactivates a user account. |
| `PUT` | `/api/users/{id}/roles` | Policy: `Roles.Assign` | Replaces role assignments for a user. |

### Roles & Permissions
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/roles` | Policy: `Roles.Read` | Lists all roles along with their granular permission mappings. |
| `GET` | `/api/permissions` | Policy: `Permissions.Read` | Lists the full catalog of securable permissions. |

### Access Requests
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/access-requests` | `AccessRequests.ViewSelf` / `AccessRequests.ViewAll` | Lists requests. Employees only see their own; Approvers/Admins view all. |
| `GET` | `/api/access-requests/{id}` | Authenticated | Retrieves access request details. Restricts viewing across users. |
| `POST` | `/api/access-requests` | Policy: `AccessRequests.Create` | Submits a new access request with status `Pending`. |
| `POST` | `/api/access-requests/{id}/approve` | Policy: `AccessRequests.Approve` | Approves request. **Enforces Self-Approval Prohibition.** |
| `POST` | `/api/access-requests/{id}/reject` | Policy: `AccessRequests.Reject` | Rejects request with recorded justification. |
| `POST` | `/api/access-requests/{id}/revoke` | Policy: `AccessRequests.Revoke` | Early revocation of an active approved grant. |

### Audit Logs
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/audit-logs` | Policy: `AuditLogsView` | Paginated chronological audit records. Immutable; no edit/delete endpoints exist. |

---

## 8. Example Request and Response Bodies

### 8.1 Login Request (`POST /api/auth/login`)
```json
{
  "username": "admin.sarah",
  "password": "Password123!"
}
```

#### Response (200 OK):
```json
{
  "success": true,
  "message": "Authentication successful.",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2026-10-21T16:30:00Z",
    "user": {
      "userId": 1,
      "fullName": "Sarah Jenkins",
      "username": "admin.sarah",
      "email": "sarah.jenkins@secureaccess.internal",
      "isActive": true,
      "roles": ["Admin", "Employee"]
    }
  },
  "errors": []
}
```

### 8.2 Create Access Request (`POST /api/access-requests`)
```json
{
  "resourceName": "PROD-PAYMENT-INGRESS-GATEWAY",
  "reason": "Emergency debugging for webhook dropoff under ticket INC-8041.",
  "startAt": "2026-10-21T15:00:00Z",
  "expiresAt": "2026-10-21T19:00:00Z"
}
```

#### Response (201 Created):
```json
{
  "success": true,
  "message": "Privileged access request submitted successfully.",
  "data": {
    "accessRequestId": 13,
    "requesterUserId": 4,
    "requesterUsername": "emp.david",
    "requesterFullName": "David Chen",
    "resourceName": "PROD-PAYMENT-INGRESS-GATEWAY",
    "reason": "Emergency debugging for webhook dropoff under ticket INC-8041.",
    "requestedAt": "2026-10-21T14:45:00Z",
    "startAt": "2026-10-21T15:00:00Z",
    "expiresAt": "2026-10-21T19:00:00Z",
    "status": "Pending",
    "approverUserId": null,
    "approverUsername": null,
    "approverFullName": null,
    "decisionAt": null,
    "decisionComment": null,
    "isActiveGrant": false
  },
  "errors": []
}
```

### 8.3 Approve Access Request (`POST /api/access-requests/13/approve`)
```json
{
  "startAt": "2026-10-21T15:00:00Z",
  "expiresAt": "2026-10-21T19:00:00Z",
  "comment": "Approved for 4 hours. Access window logged under change ticket."
}
```

#### Response (200 OK):
```json
{
  "success": true,
  "message": "Access request #13 approved successfully.",
  "data": {
    "accessRequestId": 13,
    "status": "Approved",
    "approverUserId": 2,
    "approverUsername": "approver.marcus",
    "approverFullName": "Marcus Vance",
    "decisionAt": "2026-10-21T14:48:10Z",
    "decisionComment": "Approved for 4 hours. Access window logged under change ticket.",
    "isActiveGrant": true
  },
  "errors": []
}
```

---

## 9. Automated Testing & Security Hardening (Phase 4)

The automated test suite in `SecureAccess.Api.Tests` contains **40 automated tests** validating:
- Authentication & JWT token generation with claims and signature verification.
- Password hashing and verification against Phase 1 PBKDF2 seed hashes.
- Access request workflows (create $\to$ approve $\to$ reject $\to$ revoke).
- **Self-Approval Prevention Rule:** Approvers cannot approve their own requests.
- **Self-Role Alteration Prevention:** Users cannot modify their own assigned roles.
- **Self-Deactivation Guard:** Administrators cannot deactivate their own active accounts.
- **Anti-Duplicate Submissions:** Users cannot submit duplicate pending requests for the same target resource.
- **Expiration Window Boundaries:** Requests with past expiration dates are rejected upon creation and approval.
- **Duplicate Decision Prevention:** Already decided requests cannot be re-adjudicated.
- **Time Window Validation:** Inverted windows (`StartAt > ExpiresAt`) are rejected.
- **Ownership Scoping:** Regular employees cannot inspect other users' requests.
- **Input Validation:** Negative/zero IDs return HTTP 400 Bad Request; malformed bodies return validation error lists.
- **End-to-End API Integration:** Testing 401 Unauthorized, 403 Forbidden, and 200 OK across controllers with `WebApplicationFactory`.

### Running Tests:
```powershell
dotnet test SecureAccess.sln
```

---

## 10. PAM System Scope & Limitations

1. **Access Decision Authority:** SecureAccess manages, stores, and audits access decisions, approval workflows, and time windows.
2. **Resource Boundary:** Unless an external connector (e.g. Active Directory, AWS IAM, or SSH bastion agent) is integrated, the system manages the decision state and validity window; it does not directly manage external firewall or OS root accounts.
