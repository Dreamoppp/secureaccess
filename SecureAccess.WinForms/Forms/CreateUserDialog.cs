using SecureAccess.Api.DTOs.Roles;
using SecureAccess.Api.DTOs.Users;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class CreateUserDialog : Form
{
    private readonly TextBox _txtFullName;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtEmail;
    private readonly TextBox _txtPassword;
    private readonly CheckedListBox _clbRoles;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly Label _lblError;
    private readonly List<RoleDto> _availableRoles = new();

    public CreateUserDialog()
    {
        Text = "Provision New User Account";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(480, 560);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        var lblTitle = new Label
        {
            Text = "Create Enterprise Identity",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(28, 20),
            AutoSize = true
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = UiTheme.DangerRed,
            Font = UiTheme.FontSmall,
            Location = new Point(28, 50),
            Size = new Size(410, 32),
            Visible = false
        };

        var lblFullName = new Label { Text = "Full Name *", Font = UiTheme.FontSubheader, Location = new Point(28, 86), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(28, 108), Size = new Size(410, 30), Font = new Font("Segoe UI", 10f) };

        var lblUsername = new Label { Text = "Username *", Font = UiTheme.FontSubheader, Location = new Point(28, 146), AutoSize = true };
        _txtUsername = new TextBox { Location = new Point(28, 168), Size = new Size(410, 30), Font = new Font("Segoe UI", 10f) };

        var lblEmail = new Label { Text = "Email Address *", Font = UiTheme.FontSubheader, Location = new Point(28, 206), AutoSize = true };
        _txtEmail = new TextBox { Location = new Point(28, 228), Size = new Size(410, 30), Font = new Font("Segoe UI", 10f) };

        var lblPassword = new Label { Text = "Initial Password *", Font = UiTheme.FontSubheader, Location = new Point(28, 266), AutoSize = true };
        _txtPassword = new TextBox { Location = new Point(28, 288), Size = new Size(410, 30), Font = new Font("Segoe UI", 10f), UseSystemPasswordChar = true };

        var lblRoles = new Label { Text = "Assign Roles", Font = UiTheme.FontSubheader, Location = new Point(28, 326), AutoSize = true };
        _clbRoles = new CheckedListBox
        {
            Location = new Point(28, 348),
            Size = new Size(410, 90),
            CheckOnClick = true,
            Font = UiTheme.FontBody
        };

        _btnSave = new Button { Text = "Create User", Location = new Point(218, 456), Size = new Size(110, 38) };
        UiTheme.StylePrimaryButton(_btnSave);
        _btnSave.Click += async (_, _) => await HandleSaveAsync();

        _btnCancel = new Button { Text = "Cancel", Location = new Point(338, 456), Size = new Size(100, 38), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(_btnCancel);

        Controls.AddRange(new Control[]
        {
            lblTitle, _lblError,
            lblFullName, _txtFullName,
            lblUsername, _txtUsername,
            lblEmail, _txtEmail,
            lblPassword, _txtPassword,
            lblRoles, _clbRoles,
            _btnSave, _btnCancel
        });

        Load += async (_, _) => await LoadRolesAsync();
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var roles = await ApiClient.Instance.GetRolesAsync();
            _availableRoles.Clear();
            _availableRoles.AddRange(roles);

            _clbRoles.Items.Clear();
            foreach (var role in roles)
            {
                var index = _clbRoles.Items.Add(role.RoleName);
                if (role.RoleName == "Employee")
                {
                    _clbRoles.SetItemChecked(index, true);
                }
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
        var fullName = _txtFullName.Text.Trim();
        var username = _txtUsername.Text.Trim();
        var email = _txtEmail.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "Please fill in all required fields.";
            _lblError.Visible = true;
            return;
        }

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
            await ApiClient.Instance.CreateUserAsync(new CreateUserDto
            {
                FullName = fullName,
                Username = username,
                Email = email,
                Password = password,
                RoleIds = selectedRoleIds
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            _lblError.Text = ex.Message;
            _lblError.Visible = true;
            _btnSave.Enabled = true;
            _btnSave.Text = "Create User";
        }
    }
}
