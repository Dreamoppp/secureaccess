using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class LoginForm : Form
{
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtPassword;
    private readonly TextBox _txtApiUrl;
    private readonly CheckBox _chkShowPassword;
    private readonly Button _btnLogin;
    private readonly Label _lblError;
    private readonly ProgressBar _progressBar;
    private readonly Panel _pnlError;
    private readonly LinkLabel _lnkToggleApiUrl;
    private readonly Panel _pnlApiUrl;

    public LoginForm()
    {
        Text = "SecureAccess — Privileged Access Management";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(460, 620);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        // Brand Banner Header
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            BackColor = UiTheme.SidebarBackground
        };

        var lblBrandIcon = new Label
        {
            Text = "🛡️ SECUREACCESS",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(30, 30),
            AutoSize = true
        };

        var lblBrandSubtitle = new Label
        {
            Text = "Privileged Access Management & Security Governance",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.SidebarTextMuted,
            Location = new Point(34, 70),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblBrandSubtitle);
        pnlHeader.Controls.Add(lblBrandIcon);

        // Body Panel
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36, 24, 36, 20),
            BackColor = Color.White
        };

        var lblLoginTitle = new Label
        {
            Text = "Sign In to Your Workspace",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(36, 16),
            AutoSize = true
        };

        // Error Panel
        _pnlError = new Panel
        {
            Location = new Point(36, 50),
            Size = new Size(372, 42),
            BackColor = UiTheme.DangerBg,
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        _lblError = new Label
        {
            Text = "Invalid username or password.",
            ForeColor = UiTheme.DangerRed,
            Font = UiTheme.FontSmall,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        _pnlError.Controls.Add(_lblError);

        // Username
        var lblUsername = new Label
        {
            Text = "Username or Directory ID",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(36, 102),
            AutoSize = true
        };

        _txtUsername = new TextBox
        {
            Location = new Point(36, 126),
            Size = new Size(372, 34),
            Font = new Font("Segoe UI", 11f),
            PlaceholderText = "e.g. admin.sarah, approver.marcus, emp.david"
        };

        // Password
        var lblPassword = new Label
        {
            Text = "Master Password",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(36, 172),
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Location = new Point(36, 196),
            Size = new Size(372, 34),
            Font = new Font("Segoe UI", 11f),
            UseSystemPasswordChar = true,
            PlaceholderText = "Enter your password"
        };

        _chkShowPassword = new CheckBox
        {
            Text = "Show password",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(36, 236),
            AutoSize = true
        };
        _chkShowPassword.CheckedChanged += (_, _) => _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;

        // Progress Bar (Loading State)
        _progressBar = new ProgressBar
        {
            Location = new Point(36, 268),
            Size = new Size(372, 6),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        // Sign In Button
        _btnLogin = new Button
        {
            Text = "Sign In",
            Location = new Point(36, 284),
            Size = new Size(372, 44),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        UiTheme.StylePrimaryButton(_btnLogin);
        _btnLogin.Click += async (_, _) => await HandleLoginAsync();

        // Quick Demo Fill Buttons
        var lblQuickDemo = new Label
        {
            Text = "Quick Demo Identities:",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(36, 344),
            AutoSize = true
        };

        var pnlDemoButtons = new FlowLayoutPanel
        {
            Location = new Point(36, 368),
            Size = new Size(372, 36),
            WrapContents = false
        };

        var btnFillAdmin = new Button { Text = "Admin", Width = 90, Height = 28, Font = UiTheme.FontSmall };
        UiTheme.StyleSecondaryButton(btnFillAdmin);
        btnFillAdmin.Click += (_, _) => FillDemo("admin.sarah", "Password123!");

        var btnFillApprover = new Button { Text = "Approver", Width = 90, Height = 28, Font = UiTheme.FontSmall };
        UiTheme.StyleSecondaryButton(btnFillApprover);
        btnFillApprover.Click += (_, _) => FillDemo("approver.marcus", "Password123!");

        var btnFillEmployee = new Button { Text = "Employee", Width = 90, Height = 28, Font = UiTheme.FontSmall };
        UiTheme.StyleSecondaryButton(btnFillEmployee);
        btnFillEmployee.Click += (_, _) => FillDemo("emp.david", "Password123!");

        pnlDemoButtons.Controls.AddRange(new Control[] { btnFillAdmin, btnFillApprover, btnFillEmployee });

        // Advanced API URL Configuration
        _lnkToggleApiUrl = new LinkLabel
        {
            Text = "⚙️ Configure Backend API URL",
            Font = UiTheme.FontSmall,
            LinkColor = UiTheme.PrimaryBlue,
            Location = new Point(36, 412),
            AutoSize = true
        };

        _pnlApiUrl = new Panel
        {
            Location = new Point(36, 436),
            Size = new Size(372, 34),
            Visible = false
        };

        _txtApiUrl = new TextBox
        {
            Text = ApiClient.Instance.BaseUrl,
            Dock = DockStyle.Fill,
            Font = UiTheme.FontSmall
        };
        _txtApiUrl.TextChanged += (_, _) => ApiClient.Instance.BaseUrl = _txtApiUrl.Text.Trim();
        _pnlApiUrl.Controls.Add(_txtApiUrl);

        _lnkToggleApiUrl.LinkClicked += (_, _) =>
        {
            _pnlApiUrl.Visible = !_pnlApiUrl.Visible;
            _lnkToggleApiUrl.Text = _pnlApiUrl.Visible ? "▲ Hide API URL Setting" : "⚙️ Configure Backend API URL";
        };

        pnlBody.Controls.Add(_pnlApiUrl);
        pnlBody.Controls.Add(_lnkToggleApiUrl);
        pnlBody.Controls.Add(pnlDemoButtons);
        pnlBody.Controls.Add(lblQuickDemo);
        pnlBody.Controls.Add(_btnLogin);
        pnlBody.Controls.Add(_progressBar);
        pnlBody.Controls.Add(_chkShowPassword);
        pnlBody.Controls.Add(_txtPassword);
        pnlBody.Controls.Add(lblPassword);
        pnlBody.Controls.Add(_txtUsername);
        pnlBody.Controls.Add(lblUsername);
        pnlBody.Controls.Add(_pnlError);
        pnlBody.Controls.Add(lblLoginTitle);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);

        AcceptButton = _btnLogin;
    }

    private void FillDemo(string username, string password)
    {
        _txtUsername.Text = username;
        _txtPassword.Text = password;
        _pnlError.Visible = false;
    }

    private async Task HandleLoginAsync()
    {
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username.");
            _txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password.");
            _txtPassword.Focus();
            return;
        }

        SetLoading(true);

        try
        {
            var loginResponse = await ApiClient.Instance.LoginAsync(username, password);

            // Fetch complete active user identity, roles, and granular permissions
            UserSession.Instance.SetSession(loginResponse.Token, loginResponse.ExpiresAt, new SecureAccess.Api.DTOs.Auth.CurrentUserDto
            {
                UserId = loginResponse.User.UserId,
                Username = loginResponse.User.Username,
                FullName = loginResponse.User.FullName,
                Email = loginResponse.User.Email,
                IsActive = loginResponse.User.IsActive,
                Roles = loginResponse.User.Roles,
                Permissions = new List<string>() // Populated via /api/auth/me
            });

            var meResponse = await ApiClient.Instance.GetCurrentUserAsync();
            UserSession.Instance.SetSession(loginResponse.Token, loginResponse.ExpiresAt, meResponse);

            // Authentication succeeded! Open Main Dashboard
            Hide();
            var mainForm = new MainForm();
            mainForm.FormClosed += (_, _) => Close();
            mainForm.Show();
        }
        catch (ApiException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"An unexpected error occurred: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        _progressBar.Visible = loading;
        _btnLogin.Enabled = !loading;
        _txtUsername.Enabled = !loading;
        _txtPassword.Enabled = !loading;
        _chkShowPassword.Enabled = !loading;
        _btnLogin.Text = loading ? "Authenticating..." : "Sign In";
    }

    private void ShowError(string message)
    {
        _lblError.Text = message;
        _pnlError.Visible = true;
    }
}
