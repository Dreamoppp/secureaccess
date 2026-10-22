using SecureAccess.WinForms.Helpers;

namespace SecureAccess.WinForms.Forms;

public class PromptDialog : Form
{
    private readonly TextBox _txtInput;
    private readonly Label _lblPrompt;
    private readonly Label _lblError;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string Value => _txtInput.Text.Trim();

    public PromptDialog(string title, string promptText, string defaultValue = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(460, 260);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        _lblPrompt = new Label
        {
            Text = promptText,
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, 20),
            Size = new Size(396, 44)
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = UiTheme.DangerRed,
            Font = UiTheme.FontSmall,
            Location = new Point(24, 68),
            Size = new Size(396, 20),
            Visible = false
        };

        _txtInput = new TextBox
        {
            Text = defaultValue,
            Location = new Point(24, 92),
            Size = new Size(396, 30),
            Font = new Font("Segoe UI", 10.5f)
        };

        _btnOk = new Button
        {
            Text = "Confirm",
            Location = new Point(220, 160),
            Size = new Size(100, 36)
        };
        UiTheme.StylePrimaryButton(_btnOk);
        _btnOk.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtInput.Text) || _txtInput.Text.Trim().Length < 5)
            {
                _lblError.Text = "Please provide at least 5 characters of justification.";
                _lblError.Visible = true;
                _txtInput.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(328, 160),
            Size = new Size(92, 36),
            DialogResult = DialogResult.Cancel
        };
        UiTheme.StyleSecondaryButton(_btnCancel);

        Controls.AddRange(new Control[]
        {
            _lblPrompt, _lblError, _txtInput, _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    public static string? Show(IWin32Window owner, string title, string prompt, string defaultValue = "")
    {
        using var dialog = new PromptDialog(title, prompt, defaultValue);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Value : null;
    }
}
