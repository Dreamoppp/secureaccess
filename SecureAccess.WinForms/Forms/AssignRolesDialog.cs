using SecureAccess.Api.DTOs.Roles;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class AssignRolesDialog : Form
{
    private readonly int _userId;
    private readonly CheckedListBox _clbRoles;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly Label _lblError;
    private readonly List<RoleDto> _availableRoles = new();

    public AssignRolesDialog(UserDto user)
    {
        _userId = user.UserId;

        Text = $"Assign Roles — {user.Username}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(420, 380);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        var lblTitle = new Label
        {
            Text = $"Role Assignments: {user.Username}",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Select the roles authorized for this user identity:",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(24, 48),
            AutoSize = true
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = UiTheme.DangerRed,
            Font = UiTheme.FontSmall,
            Location = new Point(24, 72),
            Size = new Size(356, 24),
            Visible = false
        };

        _clbRoles = new CheckedListBox
        {
            Location = new Point(24, 102),
            Size = new Size(356, 160),
            CheckOnClick = true,
            Font = UiTheme.FontBody
        };

        _btnSave = new Button { Text = "Save Roles", Location = new Point(190, 280), Size = new Size(100, 38) };
        UiTheme.StylePrimaryButton(_btnSave);
        _btnSave.Click += async (_, _) => await HandleSaveAsync();

        _btnCancel = new Button { Text = "Cancel", Location = new Point(300, 280), Size = new Size(80, 38), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(_btnCancel);

        Controls.AddRange(new Control[]
        {
            lblTitle, lblSubtitle, _lblError,
            _clbRoles, _btnSave, _btnCancel
        });

        Load += async (_, _) => await LoadRolesAsync(user.Roles);
    }

    private async Task LoadRolesAsync(List<string> currentRoles)
    {
        try
        {
            var roles = await ApiClient.Instance.GetRolesAsync();
            _availableRoles.Clear();
            _availableRoles.AddRange(roles);

            _clbRoles.Items.Clear();
            foreach (var role in roles)
            {
                var isAssigned = currentRoles.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase);
                _clbRoles.Items.Add(role.RoleName, isAssigned);
            }
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Failed to load roles: {ex.Message}";
            _lblError.Visible = true;
        }
    }

    private async Task HandleSaveAsync()
    {
        var selectedRoleIds = new List<int>();
        for (int i = 0; i < _clbRoles.Items.Count; i++)
        {
            if (_clbRoles.GetItemChecked(i))
            {
                var roleName = _clbRoles.Items[i].ToString();
                var role = _availableRoles.FirstOrDefault(r => r.RoleName == roleName);
                if (role != null) selectedRoleIds.Add(role.RoleId);
            }
        }

        _btnSave.Enabled = false;
        _btnSave.Text = "Saving...";

        try
        {
            await ApiClient.Instance.UpdateUserRolesAsync(_userId, selectedRoleIds);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            _lblError.Text = ex.Message;
            _lblError.Visible = true;
            _btnSave.Enabled = true;
            _btnSave.Text = "Save Roles";
        }
    }
}
