using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.DTOs.AuditLogs;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class DashboardView : UserControl
{
    private readonly StatCardControl _cardTotalUsers;
    private readonly StatCardControl _cardActiveUsers;
    private readonly StatCardControl _cardPendingRequests;
    private readonly StatCardControl _cardApprovedRequests;
    private readonly StatCardControl _cardAuditLogs;

    private readonly DataGridView _gridRecentRequests;
    private readonly DataGridView _gridRecentAudits;
    private readonly Label _lblLoading;
    private readonly Button _btnRefresh;

    public DashboardView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;
        AutoScroll = true;

        // Top Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(24, 16, 24, 8),
            BackColor = UiTheme.ContentBackground
        };

        var lblPageTitle = new Label
        {
            Text = "PAM Operational Overview",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, 16),
            AutoSize = true
        };

        var lblPageSub = new Label
        {
            Text = "Live operational telemetry, active privileged grants, and triage queue.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(24, 46),
            AutoSize = true
        };

        _btnRefresh = new Button
        {
            Text = "↻ Refresh Data",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 160, 20),
            Size = new Size(130, 36)
        };
        UiTheme.StyleSecondaryButton(_btnRefresh);
        _btnRefresh.Click += async (_, _) => await LoadDashboardDataAsync();

        _lblLoading = new Label
        {
            Text = "Refreshing telemetry...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.PrimaryBlue,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 300, 28),
            AutoSize = true,
            Visible = false
        };

        pnlHeader.Controls.AddRange(new Control[] { lblPageTitle, lblPageSub, _lblLoading, _btnRefresh });
        Controls.Add(pnlHeader);

        // Cards Flow Layout
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(20, 8, 20, 8),
            WrapContents = true,
            AutoScroll = false
        };

        _cardTotalUsers = new StatCardControl { CardTitle = "TOTAL IDENTITIES", Subtitle = "Registered accounts", AccentColor = Color.FromArgb(79, 70, 229) };
        _cardActiveUsers = new StatCardControl { CardTitle = "ACTIVE USERS", Subtitle = "Enabled accounts", AccentColor = UiTheme.SuccessGreen };
        _cardPendingRequests = new StatCardControl { CardTitle = "PENDING TRIAGE", Subtitle = "Awaiting decision", AccentColor = UiTheme.WarningAmber };
        _cardApprovedRequests = new StatCardControl { CardTitle = "APPROVED GRANTS", Subtitle = "Privileges granted", AccentColor = UiTheme.PrimaryBlue };
        _cardAuditLogs = new StatCardControl { CardTitle = "SECURITY AUDITS", Subtitle = "Logged telemetry events", AccentColor = Color.FromArgb(14, 165, 233) };

        pnlCards.Controls.AddRange(new Control[]
        {
            _cardTotalUsers, _cardActiveUsers, _cardPendingRequests, _cardApprovedRequests, _cardAuditLogs
        });
        Controls.Add(pnlCards);

        // Main Tables Container
        var pnlTables = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 8, 24, 24)
        };
        pnlTables.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        pnlTables.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        // Left Container: Recent Access Requests
        var pnlLeft = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        var lblRequestsHeader = new Label
        {
            Text = "Recent Privileged Access Requests",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };

        _gridRecentRequests = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        UiTheme.ApplyModernGridStyle(_gridRecentRequests);
        ConfigureRequestsGrid();

        pnlLeft.Controls.Add(_gridRecentRequests);
        pnlLeft.Controls.Add(lblRequestsHeader);

        // Right Container: Recent Security Audits
        var pnlRight = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        var lblAuditsHeader = new Label
        {
            Text = "Recent Security Audit Telemetry",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };

        _gridRecentAudits = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        UiTheme.ApplyModernGridStyle(_gridRecentAudits);
        ConfigureAuditsGrid();

        pnlRight.Controls.Add(_gridRecentAudits);
        pnlRight.Controls.Add(lblAuditsHeader);

        pnlTables.Controls.Add(pnlLeft, 0, 0);
        pnlTables.Controls.Add(pnlRight, 1, 0);

        Controls.Add(pnlTables);

        // Bring header and cards to front for proper docking
        pnlHeader.BringToFront();
        pnlCards.BringToFront();
        pnlTables.BringToFront();

        Load += async (_, _) => await LoadDashboardDataAsync();
        Resize += (_, _) =>
        {
            _btnRefresh.Location = new Point(Width - 160, 20);
            _lblLoading.Location = new Point(Width - 300, 28);
        };
    }

    private void ConfigureRequestsGrid()
    {
        _gridRecentRequests.Columns.Clear();
        _gridRecentRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#ID", Name = "Id", FillWeight = 12 });
        _gridRecentRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resource", Name = "Resource", FillWeight = 32 });
        _gridRecentRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requester", Name = "Requester", FillWeight = 26 });
        _gridRecentRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Name = "Status", FillWeight = 18 });
        _gridRecentRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requested", Name = "Date", FillWeight = 20 });

        _gridRecentRequests.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex == 3 && e.Value != null && e.CellStyle != null)
            {
                var (fore, _) = UiTheme.GetStatusColors(e.Value.ToString());
                e.CellStyle.ForeColor = fore;
                e.CellStyle.SelectionForeColor = fore;
                e.CellStyle.Font = UiTheme.FontBadge;
            }
        };
    }

    private void ConfigureAuditsGrid()
    {
        _gridRecentAudits.Columns.Clear();
        _gridRecentAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", Name = "Action", FillWeight = 32 });
        _gridRecentAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Actor", Name = "Actor", FillWeight = 22 });
        _gridRecentAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Timestamp (UTC)", Name = "Time", FillWeight = 26 });
        _gridRecentAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resource", Name = "Resource", FillWeight = 20 });
    }

    public async Task LoadDashboardDataAsync()
    {
        _lblLoading.Visible = true;
        _btnRefresh.Enabled = false;

        try
        {
            // 1. Fetch Users Summary if authorized
            if (UserSession.Instance.HasPermission("Users.Read"))
            {
                var usersResult = await ApiClient.Instance.GetUsersAsync(pageNumber: 1, pageSize: 100);
                _cardTotalUsers.Value = usersResult.TotalCount.ToString();
                _cardActiveUsers.Value = usersResult.Items.Count(u => u.IsActive).ToString();
            }
            else
            {
                _cardTotalUsers.Value = "—";
                _cardTotalUsers.Subtitle = "Restricted role";
                _cardActiveUsers.Value = "—";
                _cardActiveUsers.Subtitle = "Restricted role";
            }

            // 2. Fetch Access Requests
            var requestsResult = await ApiClient.Instance.GetAccessRequestsAsync(pageNumber: 1, pageSize: 50);
            var pendingCount = requestsResult.Items.Count(r => r.Status == "Pending");
            var approvedCount = requestsResult.Items.Count(r => r.Status == "Approved");

            _cardPendingRequests.Value = pendingCount.ToString();
            _cardApprovedRequests.Value = approvedCount.ToString();

            // Populate recent requests grid
            _gridRecentRequests.Rows.Clear();
            foreach (var req in requestsResult.Items.Take(10))
            {
                _gridRecentRequests.Rows.Add(
                    req.AccessRequestId,
                    req.ResourceName,
                    req.RequesterFullName,
                    req.Status,
                    $"{req.RequestedAt:MM-dd HH:mm}"
                );
            }

            // 3. Fetch Audits if authorized
            if (UserSession.Instance.HasPermission("AuditLogs.View"))
            {
                var auditsResult = await ApiClient.Instance.GetAuditLogsAsync(pageNumber: 1, pageSize: 10);
                _cardAuditLogs.Value = auditsResult.TotalCount.ToString();

                _gridRecentAudits.Rows.Clear();
                foreach (var log in auditsResult.Items)
                {
                    _gridRecentAudits.Rows.Add(
                        log.Action,
                        log.Username ?? "[SYSTEM]",
                        $"{log.CreatedAt:MM-dd HH:mm:ss}",
                        log.ResourceType
                    );
                }
            }
            else
            {
                _cardAuditLogs.Value = "—";
                _cardAuditLogs.Subtitle = "Restricted role";
                _gridRecentAudits.Rows.Clear();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to refresh dashboard: {ex.Message}", "Telemetry Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _lblLoading.Visible = false;
            _btnRefresh.Enabled = true;
        }
    }
}
