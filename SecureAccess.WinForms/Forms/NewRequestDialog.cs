using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Forms;

public class NewRequestDialog : Form
{
    private readonly TextBox _txtResource;
    private readonly TextBox _txtReason;
    private readonly DateTimePicker _dtpStart;
    private readonly DateTimePicker _dtpExpires;
    private readonly CheckBox _chkCustomDates;
    private readonly Panel _pnlDates;
    private readonly Button _btnSubmit;
    private readonly Button _btnCancel;
    private readonly Label _lblError;

    public NewRequestDialog()
    {
        Text = "Submit Privileged Access Request";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(500, 520);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        var lblTitle = new Label
        {
            Text = "New Access Request",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(28, 20),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Request temporary privileged credentials for sensitive infrastructure.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(28, 48),
            AutoSize = true
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = UiTheme.DangerRed,
            Font = UiTheme.FontSmall,
            Location = new Point(28, 72),
            Size = new Size(430, 24),
            Visible = false
        };

        var lblResource = new Label { Text = "Target Resource Name *", Font = UiTheme.FontSubheader, Location = new Point(28, 100), AutoSize = true };
        _txtResource = new TextBox
        {
            Location = new Point(28, 122),
            Size = new Size(430, 30),
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "e.g. SQL-PROD-FINANCE-01, K8S-PROD-PAYMENTS, AWS-IAM-VAULT"
        };

        var lblReason = new Label { Text = "Business Justification *", Font = UiTheme.FontSubheader, Location = new Point(28, 162), AutoSize = true };
        _txtReason = new TextBox
        {
            Location = new Point(28, 184),
            Size = new Size(430, 70),
            Font = new Font("Segoe UI", 10f),
            Multiline = true,
            PlaceholderText = "Explain why privileged access is required, ticket numbers, and intended changes."
        };

        _chkCustomDates = new CheckBox
        {
            Text = "Specify custom access window (Optional — defaults to 4 hours upon approval)",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(28, 266),
            Size = new Size(430, 24)
        };

        _pnlDates = new Panel
        {
            Location = new Point(28, 296),
            Size = new Size(430, 100),
            Visible = false
        };

        var lblStart = new Label { Text = "Access Start Time (UTC):", Font = UiTheme.FontSmall, Location = new Point(0, 4), AutoSize = true };
        _dtpStart = new DateTimePicker
        {
            Location = new Point(0, 24),
            Size = new Size(205, 26),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Value = DateTime.Now
        };

        var lblExpires = new Label { Text = "Access Expiry Time (UTC):", Font = UiTheme.FontSmall, Location = new Point(220, 4), AutoSize = true };
        _dtpExpires = new DateTimePicker
        {
            Location = new Point(220, 24),
            Size = new Size(205, 26),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Value = DateTime.Now.AddHours(4)
        };

        _pnlDates.Controls.AddRange(new Control[] { lblStart, _dtpStart, lblExpires, _dtpExpires });

        _chkCustomDates.CheckedChanged += (_, _) => _pnlDates.Visible = _chkCustomDates.Checked;

        _btnSubmit = new Button { Text = "Submit Request", Location = new Point(228, 416), Size = new Size(130, 38) };
        UiTheme.StylePrimaryButton(_btnSubmit);
        _btnSubmit.Click += async (_, _) => await HandleSubmitAsync();

        _btnCancel = new Button { Text = "Cancel", Location = new Point(368, 416), Size = new Size(90, 38), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(_btnCancel);

        Controls.AddRange(new Control[]
        {
            lblTitle, lblSubtitle, _lblError,
            lblResource, _txtResource,
            lblReason, _txtReason,
            _chkCustomDates, _pnlDates,
            _btnSubmit, _btnCancel
        });
    }

    private async Task HandleSubmitAsync()
    {
        var resource = _txtResource.Text.Trim();
        var reason = _txtReason.Text.Trim();

        if (string.IsNullOrWhiteSpace(resource))
        {
            _lblError.Text = "Target Resource Name is required.";
            _lblError.Visible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
        {
            _lblError.Text = "Please provide a detailed business justification (at least 5 characters).";
            _lblError.Visible = true;
            return;
        }

        DateTime? startAt = null;
        DateTime? expiresAt = null;

        if (_chkCustomDates.Checked)
        {
            startAt = _dtpStart.Value.ToUniversalTime();
            expiresAt = _dtpExpires.Value.ToUniversalTime();

            if (startAt >= expiresAt)
            {
                _lblError.Text = "Access start time must precede the expiration time.";
                _lblError.Visible = true;
                return;
            }
        }

        _btnSubmit.Enabled = false;
        _btnSubmit.Text = "Submitting...";

        try
        {
            await ApiClient.Instance.CreateAccessRequestAsync(new CreateAccessRequestDto
            {
                ResourceName = resource,
                Reason = reason,
                StartAt = startAt,
                ExpiresAt = expiresAt
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            _lblError.Text = ex.Message;
            _lblError.Visible = true;
            _btnSubmit.Enabled = true;
            _btnSubmit.Text = "Submit Request";
        }
    }
}
