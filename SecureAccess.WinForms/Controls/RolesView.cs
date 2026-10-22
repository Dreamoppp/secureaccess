using SecureAccess.Api.DTOs.Permissions;
using SecureAccess.Api.DTOs.Roles;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class RolesView : UserControl
{
    private readonly ListBox _lstRoles;
    private readonly Label _lblRoleTitle;
    private readonly Label _lblRoleDescription;
    private readonly DataGridView _gridPermissions;
    private readonly Label _lblStatus;
    private readonly List<RoleDto> _roles = new();
    private readonly List<PermissionDto> _allPermissions = new();

    public RolesView()
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
            Text = "Role-Based Access Control (RBAC)",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Inspect enterprise role definitions and granular resource permission mappings.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 34),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });
        Controls.Add(pnlHeader);

        // Split Container for Roles (Left) and Permissions (Right)
        var pnlContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        pnlContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280f));
        pnlContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // Left: Roles List Panel
        var pnlRoles = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        var lblRolesHeader = new Label
        {
            Text = "Enterprise Roles",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        _lstRoles = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ItemHeight = 32
        };
        _lstRoles.SelectedIndexChanged += (_, _) => OnRoleSelected();

        pnlRoles.Controls.Add(_lstRoles);
        pnlRoles.Controls.Add(lblRolesHeader);

        // Right: Permissions Panel
        var pnlPerms = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        _lblRoleTitle = new Label
        {
            Text = "Role Name",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.PrimaryBlue,
            Dock = DockStyle.Top,
            Height = 30
        };

        _lblRoleDescription = new Label
        {
            Text = "Role description will appear here.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 40
        };

        _gridPermissions = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        UiTheme.ApplyModernGridStyle(_gridPermissions);
        ConfigurePermissionsGrid();

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Text = "Loading RBAC configuration...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary
        };

        pnlPerms.Controls.Add(_gridPermissions);
        pnlPerms.Controls.Add(_lblRoleDescription);
        pnlPerms.Controls.Add(_lblRoleTitle);
        pnlPerms.Controls.Add(_lblStatus);

        pnlContainer.Controls.Add(pnlRoles, 0, 0);
        pnlContainer.Controls.Add(pnlPerms, 1, 0);

        Controls.Add(pnlContainer);

        // Header spacing
        pnlHeader.BringToFront();
        pnlContainer.BringToFront();

        Load += async (_, _) => await LoadDataAsync();
    }

    private void ConfigurePermissionsGrid()
    {
        _gridPermissions.Columns.Clear();
        _gridPermissions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Permission Name", Name = "Name", FillWeight = 30 });
        _gridPermissions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Resource", Name = "Resource", FillWeight = 25 });
        _gridPermissions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Operation Description", Name = "Desc", FillWeight = 45 });
    }

    private async Task LoadDataAsync()
    {
        _lblStatus.Text = "Retrieving roles and permissions...";

        try
        {
            var roles = await ApiClient.Instance.GetRolesAsync();
            var perms = await ApiClient.Instance.GetPermissionsAsync();

            _roles.Clear();
            _roles.AddRange(roles);

            _allPermissions.Clear();
            _allPermissions.AddRange(perms);

            _lstRoles.Items.Clear();
            foreach (var r in _roles)
            {
                _lstRoles.Items.Add(r.RoleName);
            }

            if (_lstRoles.Items.Count > 0)
            {
                _lstRoles.SelectedIndex = 0;
            }

            _lblStatus.Text = $"Loaded {_roles.Count} roles and {_allPermissions.Count} securable permissions.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Failed to load RBAC data: {ex.Message}";
        }
    }

    private void OnRoleSelected()
    {
        if (_lstRoles.SelectedIndex < 0 || _lstRoles.SelectedIndex >= _roles.Count) return;

        var selectedRole = _roles[_lstRoles.SelectedIndex];
        _lblRoleTitle.Text = $"Role: {selectedRole.RoleName}";
        _lblRoleDescription.Text = selectedRole.Description ?? "No description provided.";

        _gridPermissions.Rows.Clear();

        foreach (var permName in selectedRole.Permissions)
        {
            var p = _allPermissions.FirstOrDefault(perm => perm.PermissionName == permName);
            _gridPermissions.Rows.Add(
                permName,
                p?.Resource ?? "General",
                p?.Description ?? "Secured operation."
            );
        }
    }
}
