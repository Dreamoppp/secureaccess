using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.WinForms.Forms;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class AccessRequestsView : UserControl
{
    private readonly DataGridView _gridRequests;
    private readonly ComboBox _cmbStatusFilter;
    private readonly Button _btnNewRequest;
    private readonly Button _btnViewDetails;
    private readonly Button _btnRevoke;
    private readonly Button _btnRefresh;
    private readonly Label _lblStatusInfo;
    private readonly List<AccessRequestDto> _currentRequests = new();

    public AccessRequestsView()
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
            Text = "Privileged Access Requests",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Track time-bounded access requests, active credential sessions, and decision history.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 34),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

        // Action Toolbar
        var pnlToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblFilter = new Label { Text = "Status Filter:", Font = UiTheme.FontSmall, Location = new Point(12, 16), AutoSize = true };
        _cmbStatusFilter = new ComboBox
        {
            Location = new Point(96, 11),
            Size = new Size(130, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.FontBody
        };
        _cmbStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Approved", "Rejected", "Revoked" });
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += async (_, _) => await LoadRequestsAsync();

        _btnNewRequest = new Button { Text = "+ Submit Request", Location = new Point(240, 9), Size = new Size(140, 32) };
        UiTheme.StylePrimaryButton(_btnNewRequest);
        _btnNewRequest.Visible = UserSession.Instance.HasPermission("AccessRequests.Create");
        _btnNewRequest.Click += (_, _) => ShowNewRequestDialog();

        _btnViewDetails = new Button { Text = "View Details", Location = new Point(390, 9), Size = new Size(110, 32), Enabled = false };
        UiTheme.StyleSecondaryButton(_btnViewDetails);
        _btnViewDetails.Click += (_, _) => ShowDetailsDialog();

        _btnRevoke = new Button { Text = "Revoke Grant", Location = new Point(510, 9), Size = new Size(120, 32), Enabled = false };
        UiTheme.StyleDangerButton(_btnRevoke);
        _btnRevoke.Visible = UserSession.Instance.HasPermission("AccessRequests.Revoke");
        _btnRevoke.Click += async (_, _) => await HandleRevokeAsync();

        _btnRefresh = new Button { Text = "↻ Refresh", Location = new Point(640, 9), Size = new Size(90, 32) };
        UiTheme.StyleSecondaryButton(_btnRefresh);
        _btnRefresh.Click += async (_, _) => await LoadRequestsAsync();

        pnlToolbar.Controls.AddRange(new Control[]
        {
            lblFilter, _cmbStatusFilter,
            _btnNewRequest, _btnViewDetails, _btnRevoke,
            _btnRefresh
        });

        // Grid Container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0)
        };

        _gridRequests = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.ApplyModernGridStyle(_gridRequests);
        ConfigureGridColumns();

        _gridRequests.SelectionChanged += (_, _) => UpdateSelectionState();
        _gridRequests.CellDoubleClick += (_, _) => ShowDetailsDialog();

        _lblStatusInfo = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Text = "Loading access requests...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        pnlGridContainer.Controls.Add(_gridRequests);
        pnlGridContainer.Controls.Add(_lblStatusInfo);

        Controls.Add(pnlGridContainer);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
        Controls.Add(pnlToolbar);
        Controls.Add(pnlHeader);

        Load += async (_, _) => await LoadRequestsAsync();
    }

    private void ConfigureGridColumns()
    {
        _gridRequests.Columns.Clear();
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#ID", Name = "Id", FillWeight = 8 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Resource", Name = "Resource", FillWeight = 26 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requester", Name = "Requester", FillWeight = 20 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Name = "Status", FillWeight = 14 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requested At", Name = "RequestedAt", FillWeight = 16 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Active Window", Name = "Window", FillWeight = 16 });

        _gridRequests.CellFormatting += (s, e) =>
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

    private void UpdateSelectionState()
    {
        var hasSelection = _gridRequests.SelectedRows.Count > 0;
        _btnViewDetails.Enabled = hasSelection;

        if (hasSelection)
        {
            var req = GetSelectedRequest();
            _btnRevoke.Enabled = req != null && req.Status == "Approved";
        }
        else
        {
            _btnRevoke.Enabled = false;
        }
    }

    private AccessRequestDto? GetSelectedRequest()
    {
        if (_gridRequests.SelectedRows.Count == 0) return null;
        var cellValue = _gridRequests.SelectedRows[0].Cells["Id"].Value;
        if (cellValue is not int selectedId) return null;
        return _currentRequests.FirstOrDefault(r => r.AccessRequestId == selectedId);
    }

    public async Task LoadRequestsAsync()
    {
        _lblStatusInfo.Text = "Retrieving access requests...";
        _btnRefresh.Enabled = false;

        var status = _cmbStatusFilter.SelectedItem?.ToString();

        try
        {
            var pagedResult = await ApiClient.Instance.GetAccessRequestsAsync(
                status: status,
                pageNumber: 1,
                pageSize: 100);

            _currentRequests.Clear();
            _currentRequests.AddRange(pagedResult.Items);

            _gridRequests.Rows.Clear();
            foreach (var req in _currentRequests)
            {
                var windowText = req.IsActiveGrant
                    ? "🟢 ACTIVE"
                    : (req.Status == "Approved" ? "⚪ Expired" : "—");

                _gridRequests.Rows.Add(
                    req.AccessRequestId,
                    req.ResourceName,
                    req.RequesterFullName,
                    req.Status,
                    $"{req.RequestedAt:yyyy-MM-dd HH:mm}",
                    windowText
                );
            }

            _lblStatusInfo.Text = $"Displaying {_currentRequests.Count} of {pagedResult.TotalCount} requests.";
        }
        catch (Exception ex)
        {
            _lblStatusInfo.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
            UpdateSelectionState();
        }
    }

    private void ShowNewRequestDialog()
    {
        using var dialog = new NewRequestDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _ = LoadRequestsAsync();
        }
    }

    private void ShowDetailsDialog()
    {
        var req = GetSelectedRequest();
        if (req == null) return;

        using var dialog = new RequestDetailsDialog(req);
        dialog.ShowDialog();
    }

    private async Task HandleRevokeAsync()
    {
        var req = GetSelectedRequest();
        if (req == null || req.Status != "Approved") return;

        var reason = PromptDialog.Show(
            this,
            "Revoke Privileged Access",
            $"Provide a justification for revoking access grant #{req.AccessRequestId} ({req.ResourceName}):",
            "Emergency revocation per security protocol.");

        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            await ApiClient.Instance.RevokeAccessRequestAsync(req.AccessRequestId, new RevokeAccessRequestDto
            {
                Comment = reason.Trim()
            });

            MessageBox.Show($"Access request #{req.AccessRequestId} has been revoked immediately.",
                "Access Revoked", MessageBoxButtons.OK, MessageBoxIcon.Information);

            await LoadRequestsAsync();
        }
        catch (ApiException ex)
        {
            MessageBox.Show(ex.Message, "Revocation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
