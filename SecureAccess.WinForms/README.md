# SecureAccess — WinForms Enterprise Desktop UI (Phase 3)

## 1. System Overview

**SecureAccess.WinForms** is the primary administrative and employee desktop client for the SecureAccess Privileged Access Management (PAM) system. Built with **C# on .NET 8 (Windows Forms)**, it integrates directly with the Phase 2 ASP.NET Core Web API backend using asynchronous HTTP REST operations and JWT Bearer authentication.

In strict compliance with architectural guidelines:
- **Pure C# WinForms application:** Strictly no HTML, no CSS, no JavaScript, no browser-based UI frameworks (e.g. WebView2/Electron), and no TypeScript/TSX.
- **Zero direct database connections:** All operations communicate strictly through the authorized Phase 2 Web API; SQL Server is never accessed directly from the desktop client.

---

## 2. Visual Theme & UI/UX Design

The application features a modern enterprise desktop aesthetic designed to eliminate the default unstyled WinForms look:

- **Sidebar:** Dark navy blue (`#111827` / `#0F172A`) with highlighted active selection pills (`#2563EB`) and muted typography.
- **Content Canvas:** Clean light slate background (`#F8FAFC`) with elevated white card panels (`#FFFFFF`) and 1px crisp borders (`#E2E8F0`).
- **Typography:** Unified `Segoe UI` font hierarchy:
  - Page Titles: 16pt Bold
  - Section Headers: 12pt Bold
  - Subheaders & Table Headers: 10.5pt Bold
  - Body Text: 9.5pt Regular
  - Status Badges: 8.5pt Bold
- **Color-Coded Status Badges:**
  - `Approved` / `Active`: Emerald Green text on light mint background (`#16A34A` on `#DCFCE7`)
  - `Pending`: Amber text on light gold background (`#D97706` on `#FEF3C7`)
  - `Rejected` / `Inactive`: Crimson Red text on light red background (`#DC2626` on `#FEE2E2`)
  - `Revoked`: Slate Gray text on light gray background (`#4B5563` on `#F3F4F6`)
- **Data Grids:** Custom header styling, alternating row shading, selection highlights, and auto-sizing columns.

---

## 3. Project Structure

```
SecureAccess.WinForms/
│
├── Program.cs                      # Application entry point & High-DPI initialization
├── SecureAccess.WinForms.csproj    # .NET 8 Windows Forms project file
│
├── Services/
│   ├── ApiClient.cs                # Centralized async REST client with JWT Bearer handling
│   └── UserSession.cs              # Thread-safe in-memory session & permission evaluator
│
├── Helpers/
│   └── UiTheme.cs                  # Colors, fonts, and DataGridView styling helpers
│
├── Controls/
│   ├── StatCardControl.cs          # Reusable metric card with colored accent bar
│   ├── DashboardView.cs            # Operational telemetry cards & recent activity tables
│   ├── UsersView.cs                # Identity directory, search, status, and role management
│   ├── RolesView.cs                # Role definitions and granular permission matrices
│   ├── AccessRequestsView.cs       # Access requests grid, status filter, and details viewer
│   ├── ApprovalsView.cs            # Adjudication queue with Self-Approval prevention
│   └── AuditLogsView.cs            # Immutable audit event browser with pagination
│
└── Forms/
    ├── LoginForm.cs                # Login screen with password mask & demo identity shortcuts
    ├── MainForm.cs                 # Main shell with dark navy sidebar & role-scoped navigation
    ├── CreateUserDialog.cs         # Identity provisioning modal dialog
    ├── EditUserDialog.cs           # User profile update modal dialog
    ├── AssignRolesDialog.cs        # Role assignments modal dialog with checklist
    ├── NewRequestDialog.cs         # Privileged access request submission dialog
    ├── RequestDetailsDialog.cs     # Detailed request metadata & timeline dialog
    └── PromptDialog.cs             # Reusable styled modal input dialog for revocation justifications
```

---

## 4. Implemented Screens and Features

### 4.1 Login Screen (`LoginForm.cs`)
- Enterprise branding with shield emblem.
- Username and password inputs with password masking (`●●●●●●`).
- "Show password" checkbox toggle.
- Non-blocking asynchronous authentication with progress bar and loading feedback.
- Clean error banner for invalid credentials or server connectivity issues.
- **Quick Demo Fill Buttons:** One-click shortcuts for testing `Admin` (`admin.sarah`), `Approver` (`approver.marcus`), and `Employee` (`emp.david`) with default demo password `Password123!`.
- Expandable **API URL configuration** panel allowing testing against custom ports (e.g. `http://localhost:5154` or `https://localhost:7154`).

### 4.2 Main Application Shell (`MainForm.cs`)
- **Header Bar:** Active screen title, user profile badge (`FullName` + `Username`), and colored role indicator pill (`ADMIN`, `APPROVER`, `EMPLOYEE`).
- **Sidebar Navigation:**
  - Role-scoped visibility: Navigation buttons only appear if the authenticated user possesses the corresponding permission.
  - `Dashboard`: Available to all users.
  - `Identities & Users`: Displayed if user has `Users.Read`.
  - `Roles & Rights`: Displayed if user has `Roles.Read`.
  - `Access Requests`: Displayed if user has request view permissions.
  - `Approvals Queue`: Displayed if user has `AccessRequests.Approve` or `AccessRequests.Reject`.
  - `Audit Telemetry`: Displayed if user has `AuditLogs.View`.
- **Session Expiration Handler:** Subscribes to `ApiClient.SessionExpired`. If token expires, displays an alert, wipes memory state, and returns to `LoginForm`.
- **Sign Out:** Prompts for confirmation, wipes memory state, and returns to `LoginForm`.

### 4.3 Operational Dashboard (`DashboardView.cs`)
- Five metric cards:
  1. *Total Identities*
  2. *Active Users*
  3. *Pending Triage*
  4. *Approved Grants*
  5. *Security Audits*
- **Recent Access Requests Table:** Real-time triage view with status badges.
- **Recent Security Audits Table:** Chronological compliance events.
- Manual refresh action with loading indicator.

### 4.4 User Management (`UsersView.cs`)
- Search by name, username, or email.
- Active/Inactive status filtering.
- **Provision User Dialog (`CreateUserDialog.cs`):** Provisions new accounts with role assignments.
- **Edit Details Dialog (`EditUserDialog.cs`):** Updates profile information.
- **Toggle Status Button:** Activates/deactivates accounts with confirmation.
- **Assign Roles Dialog (`AssignRolesDialog.cs`):** Updates user role assignments.

### 4.5 Roles & Permissions (`RolesView.cs`)
- Two-column view:
  - Left: Role catalog (`Admin`, `Approver`, `Employee`) with descriptions.
  - Right: DataGrid showing all granular securable permissions, target resources, and operation descriptions assigned to the selected role.

### 4.6 Access Requests (`AccessRequestsView.cs`)
- DataGrid displaying Request ID, Resource Name, Requester, Status, Requested At, and Active Grant Window (`🟢 ACTIVE` vs `⚪ Expired`).
- Status filter dropdown (`All`, `Pending`, `Approved`, `Rejected`, `Revoked`).
- **Submit Request Dialog (`NewRequestDialog.cs`):** Validates resource name, business justification, and optional date window.
- **Request Details Dialog (`RequestDetailsDialog.cs`):** Displays lifecycle timeline, requester, approver, decision comments, and grant validity.
- **Revoke Grant Button:** Initiates immediate session revocation for active approved grants with justification.

### 4.7 Approvals Queue (`ApprovalsView.cs`)
- Dedicated triage screen for Approvers and Admins.
- Left: Pending requests table sorted by urgency.
- Right: Detailed adjudication panel:
  - Requester identity, requested resource, submission timestamp, and requested access window.
  - Business justification box.
  - Decision comments box.
  - **Self-Approval Prohibition:** If the logged-in approver is the requester of the selected request, the screen displays a warning banner (`"⚠️ Self-Approval Violation: You cannot approve your own request."`) and automatically disables the **Approve** button.
  - Confirmation prompts before submitting decisions.

### 4.8 Security Audit Log (`AuditLogsView.cs`)
- Forensic audit log viewer displaying Timestamp, Actor, Action, Resource Type, Resource ID, Details, and IP Address.
- Filter by action and resource type.
- Pagination controls: `Previous`, `Next`, and `Page X of Y`.
- **Strictly Read-Only:** No mutation or deletion controls exist.

---

## 5. How to Run the Complete Solution

### Step 1: Start the Backend API
Open a PowerShell terminal and launch the Phase 2 Web API:
```powershell
cd SecureAccess.Api
dotnet run
```
The API will start listening on `http://localhost:5154` (and `https://localhost:7154`).

### Step 2: Run the WinForms Desktop UI
Open a second terminal window and run:
```powershell
cd SecureAccess.WinForms
dotnet run
```

### Step 3: Log In Using Demo Accounts
Use any of the seeded identities (Password: `Password123!`):
- **Administrator:** `admin.sarah` (Full access to all screens, users, roles, and audits)
- **Approver:** `approver.marcus` (Access to Dashboard, Approvals Queue, Requests, and Audits)
- **Employee:** `emp.david` (Access to Dashboard and personal Access Requests)
