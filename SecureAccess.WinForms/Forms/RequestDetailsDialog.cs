using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.WinForms.Helpers;

namespace SecureAccess.WinForms.Forms;

public class RequestDetailsDialog : Form
{
    public RequestDetailsDialog(AccessRequestDto request)
    {
        Text = $"Access Request Details — #{request.AccessRequestId}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(540, 580);
        BackColor = Color.White;
        Font = UiTheme.FontBody;

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 16, 24, 16)
        };

        var lblHeaderId = new Label
        {
            Text = $"Access Request #{request.AccessRequestId}",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, 16),
            AutoSize = true
        };

        var (statusText, statusBg) = UiTheme.GetStatusColors(request.Status);
        var lblStatus = new Label
        {
            Text = $"  {request.Status.ToUpperInvariant()}  ",
            Font = UiTheme.FontBadge,
            ForeColor = statusText,
            BackColor = statusBg,
            Location = new Point(360, 18),
            AutoSize = true,
            Padding = new Padding(4, 3, 4, 3)
        };

        pnlHeader.Controls.AddRange(new Control[] { lblHeaderId, lblStatus });

        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 16)
        };

        int y = 16;
        void AddDetailRow(string label, string value, bool isBold = false)
        {
            var lblCaption = new Label
            {
                Text = label,
                Font = UiTheme.FontSmall,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(24, y),
                Size = new Size(160, 22)
            };

            var lblValue = new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                Font = isBold ? UiTheme.FontBodyBold : UiTheme.FontBody,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(190, y),
                Size = new Size(300, 22),
                AutoEllipsis = true
            };

            pnlBody.Controls.Add(lblCaption);
            pnlBody.Controls.Add(lblValue);
            y += 28;
        }

        AddDetailRow("Target Resource:", request.ResourceName, isBold: true);
        AddDetailRow("Requester:", $"{request.RequesterFullName} ({request.RequesterUsername})");
        AddDetailRow("Requested At (UTC):", $"{request.RequestedAt:yyyy-MM-dd HH:mm:ss} UTC");
        AddDetailRow("Start Time (UTC):", request.StartAt.HasValue ? $"{request.StartAt.Value:yyyy-MM-dd HH:mm:ss} UTC" : "Upon approval");
        AddDetailRow("Expiration Time (UTC):", request.ExpiresAt.HasValue ? $"{request.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC" : "Standard PAM window");
        AddDetailRow("Active Grant Window:", request.IsActiveGrant ? "🟢 Currently Active (In Window)" : "⚪ Inactive / Outside Window");

        y += 8;
        var lblReasonHeader = new Label
        {
            Text = "Business Justification:",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(24, y),
            AutoSize = true
        };
        pnlBody.Controls.Add(lblReasonHeader);
        y += 24;

        var txtReason = new TextBox
        {
            Text = request.Reason,
            Location = new Point(24, y),
            Size = new Size(466, 60),
            Multiline = true,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = UiTheme.FontBody
        };
        pnlBody.Controls.Add(txtReason);
        y += 72;

        if (!string.IsNullOrWhiteSpace(request.ApproverUsername))
        {
            AddDetailRow("Adjudicated By:", $"{request.ApproverFullName} ({request.ApproverUsername})");
            AddDetailRow("Decision Timestamp:", $"{request.DecisionAt:yyyy-MM-dd HH:mm:ss} UTC");

            var lblCommentHeader = new Label
            {
                Text = "Decision Comments:",
                Font = UiTheme.FontSubheader,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(24, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(lblCommentHeader);
            y += 24;

            var txtComment = new TextBox
            {
                Text = request.DecisionComment ?? "None provided.",
                Location = new Point(24, y),
                Size = new Size(466, 48),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = UiTheme.FontBody
            };
            pnlBody.Controls.Add(txtComment);
            y += 56;
        }

        var btnClose = new Button
        {
            Text = "Close",
            Location = new Point(390, y + 10),
            Size = new Size(100, 36),
            DialogResult = DialogResult.OK
        };
        UiTheme.StylePrimaryButton(btnClose);
        pnlBody.Controls.Add(btnClose);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);
    }
}
