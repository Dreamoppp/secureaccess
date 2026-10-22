using SecureAccess.Api.DTOs.Users;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class EditUserDialog : Form
{
    private readonly int _userId;
    private readonly TextBox _txtFullName;
    private readonly TextBox _txtEmail;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly Label _lblError;

    public EditUserDialog(UserDto user)
    {
        _userId = user.UserId;

        Text = $"Edit Profile — {user.Username}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(460, 340);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        var lblTitle = new Label
        {
            Text = $"Edit Profile: {user.Username}",
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
            Size = new Size(390, 24),
            Visible = false
        };

        var lblFullName = new Label { Text = "Full Name *", Font = UiTheme.FontSubheader, Location = new Point(28, 80), AutoSize = true };
        _txtFullName = new TextBox
        {
            Text = user.FullName,
            Location = new Point(28, 104),
            Size = new Size(390, 30),
            Font = new Font("Segoe UI", 10f)
        };

        var lblEmail = new Label { Text = "Email Address *", Font = UiTheme.FontSubheader, Location = new Point(28, 144), AutoSize = true };
        _txtEmail = new TextBox
        {
            Text = user.Email,
            Location = new Point(28, 168),
            Size = new Size(390, 30),
            Font = new Font("Segoe UI", 10f)
        };

        _btnSave = new Button { Text = "Save Changes", Location = new Point(208, 230), Size = new Size(115, 38) };
        UiTheme.StylePrimaryButton(_btnSave);
        _btnSave.Click += async (_, _) => await HandleSaveAsync();

        _btnCancel = new Button { Text = "Cancel", Location = new Point(333, 230), Size = new Size(85, 38), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(_btnCancel);

        Controls.AddRange(new Control[]
        {
            lblTitle, _lblError,
            lblFullName, _txtFullName,
            lblEmail, _txtEmail,
            _btnSave, _btnCancel
        });
    }

    private async Task HandleSaveAsync()
    {
        var fullName = _txtFullName.Text.Trim();
        var email = _txtEmail.Text.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            _lblError.Text = "Full Name and Email are required.";
            _lblError.Visible = true;
            return;
        }

        _btnSave.Enabled = false;
        _btnSave.Text = "Saving...";

        try
        {
            await ApiClient.Instance.UpdateUserAsync(_userId, new UpdateUserDto
            {
                FullName = fullName,
                Email = email
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            _lblError.Text = ex.Message;
            _lblError.Visible = true;
            _btnSave.Enabled = true;
            _btnSave.Text = "Save Changes";
        }
    }
}
