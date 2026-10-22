using SecureAccess.WinForms.Controls;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class MainForm : Form
{
    private readonly Panel _pnlSidebar;
    private readonly Panel _pnlHeader;
    private readonly Panel _pnlContent;
    private readonly Label _lblHeaderTitle;
    private readonly Label _lblUserBadge;
    private readonly Label _lblRoleBadge;

    private readonly Button _btnNavDashboard;
    private readonly Button? _btnNavUsers;
    private readonly Button? _btnNavRoles;
    private readonly Button? _btnNavRequests;
    private readonly Button? _btnNavApprovals;
    private readonly Button? _btnNavAudits;

    private readonly List<Button> _navButtons = new();
    private Control? _currentView;

    public MainForm()
    {
        Text = "SecureAccess — Enterprise Privileged Access Management";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1280, 820);
        BackColor = UiTheme.ContentBackground;
        Font = UiTheme.FontBody;

        // ---------------------------------------------------------------------
        // 1. Left Dark Navy Sidebar
        // ---------------------------------------------------------------------
        _pnlSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = UiTheme.SidebarBackground
        };

        // Sidebar Brand Logo / Header
        var pnlBrand = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = UiTheme.SidebarHeader,
            Padding = new Padding(20, 16, 20, 16)
        };

        var lblBrand = new Label
        {
            Text = "🛡️ SECUREACCESS",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlBrand.Controls.Add(lblBrand);
        _pnlSidebar.Controls.Add(pnlBrand);

        // Navigation Items Container
        var pnlNavList = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 16, 12, 16),
            AutoScroll = true
        };

        int navY = 16;
        Button CreateNavButton(string text, string icon, Action onClick)
        {
            var btn = new Button
            {
                Text = $"  {icon}  {text}",
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(12, navY),
                Size = new Size(216, 42),
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.FontBodyBold,
                ForeColor = UiTheme.SidebarText,
                BackColor = UiTheme.SidebarBackground,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            btn.Click += (_, _) =>
            {
                SetActiveNav(btn);
                onClick();
            };

            pnlNavList.Controls.Add(btn);
            _navButtons.Add(btn);
            navY += 48;
            return btn;
        }

        // Add role-appropriate navigation buttons
        _btnNavDashboard = CreateNavButton("Dashboard", "📊", () => SwitchView(new DashboardView(), "Operational Dashboard"));

        if (UserSession.Instance.HasPermission("Users.Read"))
        {
            _btnNavUsers = CreateNavButton("Identities & Users", "👥", () => SwitchView(new UsersView(), "Identity & User Management"));
        }

        if (UserSession.Instance.HasPermission("Roles.Read"))
        {
            _btnNavRoles = CreateNavButton("Roles & Rights", "🛡️", () => SwitchView(new RolesView(), "Role-Based Access Control"));
        }

        if (UserSession.Instance.HasPermission("AccessRequests.Create") || UserSession.Instance.HasPermission("AccessRequests.ViewSelf") || UserSession.Instance.HasPermission("AccessRequests.ViewAll"))
        {
            _btnNavRequests = CreateNavButton("Access Requests", "📋", () => SwitchView(new AccessRequestsView(), "Privileged Access Requests"));
        }

        if (UserSession.Instance.HasPermission("AccessRequests.Approve") || UserSession.Instance.HasPermission("AccessRequests.Reject"))
        {
            _btnNavApprovals = CreateNavButton("Approvals Queue", "⚖️", () => SwitchView(new ApprovalsView(), "Access Adjudication Queue"));
        }

        if (UserSession.Instance.HasPermission("AuditLogs.View"))
        {
            _btnNavAudits = CreateNavButton("Audit Telemetry", "📜", () => SwitchView(new AuditLogsView(), "Immutable Security Audit Log"));
        }

        _pnlSidebar.Controls.Add(pnlNavList);

        // Sidebar Footer (Logged in profile snippet & Logout)
        var pnlUserFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            BackColor = UiTheme.SidebarHeader,
            Padding = new Padding(16, 12, 16, 12)
        };

        var lblFooterUser = new Label
        {
            Text = UserSession.Instance.CurrentUser?.FullName ?? "Authenticated User",
            Font = UiTheme.FontBodyBold,
            ForeColor = Color.White,
            Location = new Point(16, 12),
            Size = new Size(208, 20),
            AutoEllipsis = true
        };

        var lblFooterRole = new Label
        {
            Text = string.Join(" • ", UserSession.Instance.CurrentUser?.Roles ?? new List<string> { "User" }),
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.SidebarTextMuted,
            Location = new Point(16, 34),
            Size = new Size(208, 18),
            AutoEllipsis = true
        };

        var btnFooterLogout = new Button
        {
            Text = "🚪 Sign Out",
            Location = new Point(16, 62),
            Size = new Size(208, 34),
            Font = UiTheme.FontSmall,
            Cursor = Cursors.Hand
        };
        UiTheme.StyleSecondaryButton(btnFooterLogout);
        btnFooterLogout.Click += (_, _) => HandleLogout();

        pnlUserFooter.Controls.AddRange(new Control[] { lblFooterUser, lblFooterRole, btnFooterLogout });
        _pnlSidebar.Controls.Add(pnlUserFooter);

        // ---------------------------------------------------------------------
        // 2. Top Header Bar
        // ---------------------------------------------------------------------
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(24, 16, 24, 16)
        };

        var pnlHeaderBorder = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = UiTheme.BorderColor
        };
        _pnlHeader.Controls.Add(pnlHeaderBorder);

        _lblHeaderTitle = new Label
        {
            Text = "Operational Dashboard",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, 18),
            AutoSize = true
        };
        _pnlHeader.Controls.Add(_lblHeaderTitle);

        // Right side user pill in header
        var pnlUserPill = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 480, 12),
            Size = new Size(440, 40)
        };

        _lblUserBadge = new Label
        {
            Text = $"👤 {UserSession.Instance.CurrentUser?.FullName} ({UserSession.Instance.CurrentUser?.Username})",
            Font = UiTheme.FontBodyBold,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(10, 10),
            AutoSize = true
        };

        var roleName = UserSession.Instance.CurrentUser?.Roles.FirstOrDefault() ?? "Employee";
        var (roleFore, roleBack) = UiTheme.GetStatusColors(roleName == "Admin" ? "Active" : (roleName == "Approver" ? "Pending" : null));

        _lblRoleBadge = new Label
        {
            Text = $"  {roleName.ToUpperInvariant()}  ",
            Font = UiTheme.FontBadge,
            ForeColor = roleFore,
            BackColor = roleBack,
            Location = new Point(320, 9),
            AutoSize = true,
            Padding = new Padding(4, 3, 4, 3)
        };

        pnlUserPill.Controls.AddRange(new Control[] { _lblUserBadge, _lblRoleBadge });
        _pnlHeader.Controls.Add(pnlUserPill);

        // ---------------------------------------------------------------------
        // 3. Main Dynamic Content Canvas
        // ---------------------------------------------------------------------
        _pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.ContentBackground
        };

        Controls.Add(_pnlContent);
        Controls.Add(_pnlHeader);
        Controls.Add(_pnlSidebar);

        // Setup session expired handling
        ApiClient.Instance.SessionExpired += OnSessionExpired;

        // Start on Dashboard
        SetActiveNav(_btnNavDashboard);
        SwitchView(new DashboardView(), "Operational Dashboard");

        Resize += (_, _) =>
        {
            pnlUserPill.Location = new Point(Width - 480, 12);
        };
    }

    private void SetActiveNav(Button activeButton)
    {
        foreach (var btn in _navButtons)
        {
            btn.BackColor = UiTheme.SidebarBackground;
            btn.ForeColor = UiTheme.SidebarText;
        }

        activeButton.BackColor = UiTheme.SidebarItemSelected;
        activeButton.ForeColor = Color.White;
    }

    private void SwitchView(Control newView, string title)
    {
        _lblHeaderTitle.Text = title;

        _pnlContent.SuspendLayout();
        _pnlContent.Controls.Clear();

        _currentView?.Dispose();
        _currentView = newView;
        newView.Dock = DockStyle.Fill;
        _pnlContent.Controls.Add(newView);

        _pnlContent.ResumeLayout();
    }

    private void HandleLogout()
    {
        var confirm = MessageBox.Show(
            "Are you sure you want to sign out of SecureAccess?",
            "Sign Out",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        UserSession.Instance.Clear();
        ApiClient.Instance.SessionExpired -= OnSessionExpired;

        Hide();
        var loginForm = new LoginForm();
        loginForm.FormClosed += (_, _) => Close();
        loginForm.Show();
    }

    private void OnSessionExpired()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(OnSessionExpired));
            return;
        }

        MessageBox.Show("Your session has expired. Please sign in again.", "Session Expired", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        HandleLogout();
    }
}
