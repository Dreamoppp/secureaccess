using SecureAccess.Api.DTOs.AuditLogs;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class AuditLogsView : UserControl
{
    private readonly DataGridView _gridAudits;
    private readonly TextBox _txtActionFilter;
    private readonly ComboBox _cmbResourceFilter;
    private readonly Button _btnSearch;
    private readonly Button _btnPrevPage;
    private readonly Button _btnNextPage;
    private readonly Label _lblPageInfo;
    private readonly Button _btnRefresh;
    private readonly Label _lblStatus;

    private int _currentPage = 1;
    private const int PageSize = 25;
    private int _totalPages = 1;

    public AuditLogsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;
        Padding = new Padding(24, 16, 24, 20);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = UiTheme.ContentBackground
        };

        var lblTitle = new Label
        {
            Text = "Immutable Security Audit Log",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Forensic event audit history: authentication events, privileged elevations, and administrative modifications.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 34),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

        // Filter Toolbar
        var pnlToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblAction = new Label { Text = "Action:", Font = UiTheme.FontSmall, Location = new Point(12, 16), AutoSize = true };
        _txtActionFilter = new TextBox
        {
            Location = new Point(62, 11),
            Size = new Size(180, 28),
            Font = UiTheme.FontBody,
            PlaceholderText = "e.g. LOGIN, APPROVED"
        };
        _txtActionFilter.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { _currentPage = 1; await LoadAuditLogsAsync(); }
        };

        var lblResource = new Label { Text = "Resource Type:", Font = UiTheme.FontSmall, Location = new Point(256, 16), AutoSize = true };
        _cmbResourceFilter = new ComboBox
        {
            Location = new Point(356, 11),
            Size = new Size(140, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.FontBody
        };
        _cmbResourceFilter.Items.AddRange(new object[] { "All", "Authentication", "AccessRequests", "Users", "UserRoles" });
        _cmbResourceFilter.SelectedIndex = 0;
        _cmbResourceFilter.SelectedIndexChanged += async (_, _) => { _currentPage = 1; await LoadAuditLogsAsync(); };

        _btnSearch = new Button { Text = "Filter", Location = new Point(510, 9), Size = new Size(80, 32) };
        UiTheme.StyleSecondaryButton(_btnSearch);
        _btnSearch.Click += async (_, _) => { _currentPage = 1; await LoadAuditLogsAsync(); };

        _btnRefresh = new Button { Text = "↻ Refresh", Location = new Point(600, 9), Size = new Size(90, 32) };
        UiTheme.StyleSecondaryButton(_btnRefresh);
        _btnRefresh.Click += async (_, _) => await LoadAuditLogsAsync();

        pnlToolbar.Controls.AddRange(new Control[]
        {
            lblAction, _txtActionFilter,
            lblResource, _cmbResourceFilter,
            _btnSearch, _btnRefresh
        });

        // Grid Container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0)
        };

        _gridAudits = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.ApplyModernGridStyle(_gridAudits);
        ConfigureGridColumns();

        // Pagination Bar at Bottom
        var pnlPagination = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12, 6, 12, 6)
        };

        _lblStatus = new Label
        {
            Dock = DockStyle.Left,
            Text = "Loading audit logs...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        };

        _btnPrevPage = new Button { Text = "◄ Prev", Dock = DockStyle.Right, Width = 75, Enabled = false };
        UiTheme.StyleSecondaryButton(_btnPrevPage);
        _btnPrevPage.Click += async (_, _) =>
        {
            if (_currentPage > 1) { _currentPage--; await LoadAuditLogsAsync(); }
        };

        _lblPageInfo = new Label
        {
            Text = "Page 1 of 1",
            Dock = DockStyle.Right,
            Width = 110,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextPrimary
        };

        _btnNextPage = new Button { Text = "Next ►", Dock = DockStyle.Right, Width = 75, Enabled = false };
        UiTheme.StyleSecondaryButton(_btnNextPage);
        _btnNextPage.Click += async (_, _) =>
        {
            if (_currentPage < _totalPages) { _currentPage++; await LoadAuditLogsAsync(); }
        };

        pnlPagination.Controls.AddRange(new Control[] { _lblStatus, _btnPrevPage, _lblPageInfo, _btnNextPage });

        pnlGridContainer.Controls.Add(_gridAudits);
        pnlGridContainer.Controls.Add(pnlPagination);

        Controls.Add(pnlGridContainer);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
        Controls.Add(pnlToolbar);
        Controls.Add(pnlHeader);

        Load += async (_, _) => await LoadAuditLogsAsync();
    }

    private void ConfigureGridColumns()
    {
        _gridAudits.Columns.Clear();
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#ID", Name = "Id", FillWeight = 8 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Timestamp (UTC)", Name = "Time", FillWeight = 18 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Initiator / Actor", Name = "Actor", FillWeight = 16 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Security Action", Name = "Action", FillWeight = 22 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Entity", Name = "Resource", FillWeight = 14 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", Name = "Details", FillWeight = 32 });
        _gridAudits.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP Address", Name = "IP", FillWeight = 12 });
    }

    public async Task LoadAuditLogsAsync()
    {
        _lblStatus.Text = "Retrieving compliance audit records...";
        _btnRefresh.Enabled = false;

        var action = _txtActionFilter.Text.Trim();
        var resourceType = _cmbResourceFilter.SelectedItem?.ToString();
        if (resourceType == "All") resourceType = null;

        try
        {
            var pagedResult = await ApiClient.Instance.GetAuditLogsAsync(
                pageNumber: _currentPage,
                pageSize: PageSize,
                action: string.IsNullOrWhiteSpace(action) ? null : action,
                resourceType: resourceType);

            _totalPages = Math.Max(1, pagedResult.TotalPages);
            _lblPageInfo.Text = $"Page {_currentPage} of {_totalPages}";

            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < _totalPages;

            _gridAudits.Rows.Clear();
            foreach (var log in pagedResult.Items)
            {
                _gridAudits.Rows.Add(
                    log.AuditLogId,
                    $"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}",
                    log.Username ?? "[SYSTEM / UNAUTH]",
                    log.Action,
                    log.ResourceType,
                    log.Details,
                    log.IpAddress ?? "—"
                );
            }

            _lblStatus.Text = $"Retrieved {pagedResult.Items.Count} audit records (Total in system: {pagedResult.TotalCount}). Read-only mode.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }
}
