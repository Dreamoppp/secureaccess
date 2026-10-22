using SecureAccess.Api.DTOs.Users;
using SecureAccess.WinForms.Forms;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class UsersView : UserControl
{
    private readonly DataGridView _gridUsers;
    private readonly TextBox _txtSearch;
    private readonly ComboBox _cmbStatus;
    private readonly Button _btnNewUser;
    private readonly Button _btnEditUser;
    private readonly Button _btnToggleStatus;
    private readonly Button _btnAssignRoles;
    private readonly Button _btnRefresh;
    private readonly Label _lblStatusInfo;
    private readonly List<UserDto> _currentUsers = new();

    public UsersView()
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
            Text = "Identity & User Management",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Provision enterprise accounts, maintain active directory statuses, and assign PAM roles.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 34),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

        // Action & Filter Toolbar Panel
        var pnlToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblSearch = new Label { Text = "Search:", Font = UiTheme.FontSmall, Location = new Point(12, 16), AutoSize = true };
        _txtSearch = new TextBox
        {
            Location = new Point(64, 11),
            Size = new Size(180, 28),
            Font = UiTheme.FontBody,
            PlaceholderText = "Username, Name, Email"
        };
        _txtSearch.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) await LoadUsersAsync();
        };

        var lblStatusFilter = new Label { Text = "Status:", Font = UiTheme.FontSmall, Location = new Point(260, 16), AutoSize = true };
        _cmbStatus = new ComboBox
        {
            Location = new Point(310, 11),
            Size = new Size(110, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.FontBody
        };
        _cmbStatus.Items.AddRange(new object[] { "All", "Active", "Inactive" });
        _cmbStatus.SelectedIndex = 0;
        _cmbStatus.SelectedIndexChanged += async (_, _) => await LoadUsersAsync();

        var btnSearch = new Button { Text = "Search", Location = new Point(430, 9), Size = new Size(80, 32) };
        UiTheme.StyleSecondaryButton(btnSearch);
        btnSearch.Click += async (_, _) => await LoadUsersAsync();

        _btnNewUser = new Button { Text = "+ Provision User", Location = new Point(520, 9), Size = new Size(130, 32) };
        UiTheme.StylePrimaryButton(_btnNewUser);
        _btnNewUser.Visible = UserSession.Instance.HasPermission("Users.Create");
        _btnNewUser.Click += (_, _) => ShowCreateUserDialog();

        _btnEditUser = new Button { Text = "Edit Profile", Location = new Point(660, 9), Size = new Size(100, 32), Enabled = false };
        UiTheme.StyleSecondaryButton(_btnEditUser);
        _btnEditUser.Visible = UserSession.Instance.HasPermission("Users.Update");
        _btnEditUser.Click += (_, _) => ShowEditUserDialog();

        _btnToggleStatus = new Button { Text = "Toggle Status", Location = new Point(770, 9), Size = new Size(110, 32), Enabled = false };
        UiTheme.StyleSecondaryButton(_btnToggleStatus);
        _btnToggleStatus.Visible = UserSession.Instance.HasPermission("Users.Deactivate");
        _btnToggleStatus.Click += async (_, _) => await ToggleUserStatusAsync();

        _btnAssignRoles = new Button { Text = "Assign Roles", Location = new Point(890, 9), Size = new Size(110, 32), Enabled = false };
        UiTheme.StyleSecondaryButton(_btnAssignRoles);
        _btnAssignRoles.Visible = UserSession.Instance.HasPermission("Roles.Assign");
        _btnAssignRoles.Click += (_, _) => ShowAssignRolesDialog();

        _btnRefresh = new Button { Text = "↻ Refresh", Location = new Point(1010, 9), Size = new Size(90, 32) };
        UiTheme.StyleSecondaryButton(_btnRefresh);
        _btnRefresh.Click += async (_, _) => await LoadUsersAsync();

        pnlToolbar.Controls.AddRange(new Control[]
        {
            lblSearch, _txtSearch,
            lblStatusFilter, _cmbStatus, btnSearch,
            _btnNewUser, _btnEditUser, _btnToggleStatus, _btnAssignRoles,
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

        _gridUsers = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.ApplyModernGridStyle(_gridUsers);
        ConfigureGridColumns();

        _gridUsers.SelectionChanged += (_, _) => UpdateSelectionState();

        _lblStatusInfo = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Text = "Loading users directory...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        pnlGridContainer.Controls.Add(_gridUsers);
        pnlGridContainer.Controls.Add(_lblStatusInfo);

        Controls.Add(pnlGridContainer);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 }); // Spacing
        Controls.Add(pnlToolbar);
        Controls.Add(pnlHeader);

        Load += async (_, _) => await LoadUsersAsync();
    }

    private void ConfigureGridColumns()
    {
        _gridUsers.Columns.Clear();
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#ID", Name = "UserId", FillWeight = 8 });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Username", Name = "Username", FillWeight = 18 });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Full Name", Name = "FullName", FillWeight = 24 });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Email Address", Name = "Email", FillWeight = 26 });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Name = "Status", FillWeight = 12 });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Assigned Roles", Name = "Roles", FillWeight = 22 });

        _gridUsers.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex == 4 && e.Value != null && e.CellStyle != null)
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
        var hasSelection = _gridUsers.SelectedRows.Count > 0;
        _btnEditUser.Enabled = hasSelection;
        _btnToggleStatus.Enabled = hasSelection;
        _btnAssignRoles.Enabled = hasSelection;
    }

    private UserDto? GetSelectedUser()
    {
        if (_gridUsers.SelectedRows.Count == 0) return null;
        var cellValue = _gridUsers.SelectedRows[0].Cells["UserId"].Value;
        if (cellValue is not int selectedId) return null;
        return _currentUsers.FirstOrDefault(u => u.UserId == selectedId);
    }

    public async Task LoadUsersAsync()
    {
        _lblStatusInfo.Text = "Retrieving directory accounts...";
        _btnRefresh.Enabled = false;

        bool? isActiveFilter = _cmbStatus.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null
        };

        var search = _txtSearch.Text.Trim();

        try
        {
            var pagedResult = await ApiClient.Instance.GetUsersAsync(
                pageNumber: 1,
                pageSize: 100,
                isActive: isActiveFilter,
                search: string.IsNullOrWhiteSpace(search) ? null : search);

            _currentUsers.Clear();
            _currentUsers.AddRange(pagedResult.Items);

            _gridUsers.Rows.Clear();
            foreach (var user in _currentUsers)
            {
                _gridUsers.Rows.Add(
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    user.IsActive ? "Active" : "Inactive",
                    string.Join(", ", user.Roles)
                );
            }

            _lblStatusInfo.Text = $"Displaying {_currentUsers.Count} of {pagedResult.TotalCount} enterprise identities.";
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

    private void ShowCreateUserDialog()
    {
        using var dialog = new CreateUserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _ = LoadUsersAsync();
        }
    }

    private void ShowEditUserDialog()
    {
        var user = GetSelectedUser();
        if (user == null) return;

        using var dialog = new EditUserDialog(user);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _ = LoadUsersAsync();
        }
    }

    private void ShowAssignRolesDialog()
    {
        var user = GetSelectedUser();
        if (user == null) return;

        using var dialog = new AssignRolesDialog(user);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _ = LoadUsersAsync();
        }
    }

    private async Task ToggleUserStatusAsync()
    {
        var user = GetSelectedUser();
        if (user == null) return;

        var newStatus = !user.IsActive;
        var actionText = newStatus ? "activate" : "deactivate";

        var confirm = MessageBox.Show(
            $"Are you sure you want to {actionText} the user account '{user.Username}'?",
            "Confirm Status Change",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            await ApiClient.Instance.UpdateUserStatusAsync(user.UserId, newStatus);
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            MessageBox.Show(ex.Message, "Status Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
