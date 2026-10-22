using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.WinForms.Helpers;
using SecureAccess.WinForms.Services;

namespace SecureAccess.WinForms.Controls;

public class ApprovalsView : UserControl
{
    private readonly DataGridView _gridPending;
    private readonly Label _lblSelectedTitle;
    private readonly Label _lblRequesterInfo;
    private readonly Label _lblResourceInfo;
    private readonly Label _lblRequestedAtInfo;
    private readonly Label _lblWindowInfo;
    private readonly TextBox _txtReason;
    private readonly TextBox _txtDecisionComment;
    private readonly Button _btnApprove;
    private readonly Button _btnReject;
    private readonly Button _btnRefresh;
    private readonly Label _lblWarningSelfApproval;
    private readonly Label _lblStatus;
    private readonly List<AccessRequestDto> _pendingRequests = new();

    public ApprovalsView()
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
            Text = "Privileged Access Adjudication Queue",
            Font = UiTheme.FontTitle,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Review pending privilege elevation requests, evaluate justifications, and adjudicate decisions.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 34),
            AutoSize = true
        };

        _btnRefresh = new Button
        {
            Text = "↻ Refresh Queue",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 160, 16),
            Size = new Size(130, 36)
        };
        UiTheme.StyleSecondaryButton(_btnRefresh);
        _btnRefresh.Click += async (_, _) => await LoadPendingRequestsAsync();

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, _btnRefresh });
        Controls.Add(pnlHeader);

        // Container Split: Left (Pending Queue Table), Right (Decision Panel)
        var pnlSplit = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnlSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        pnlSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        // Left Queue Panel
        var pnlQueue = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        var lblQueueTitle = new Label
        {
            Text = "Pending Access Requests",
            Font = UiTheme.FontSubheader,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        _gridPending = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.ApplyModernGridStyle(_gridPending);
        ConfigurePendingGrid();

        _gridPending.SelectionChanged += (_, _) => OnRequestSelected();

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Text = "Loading pending triage queue...",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary
        };

        pnlQueue.Controls.Add(_gridPending);
        pnlQueue.Controls.Add(lblQueueTitle);
        pnlQueue.Controls.Add(_lblStatus);

        // Right Decision Panel
        var pnlDecision = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(20),
            AutoScroll = true
        };

        _lblSelectedTitle = new Label
        {
            Text = "Select a request to review",
            Font = UiTheme.FontSection,
            ForeColor = UiTheme.PrimaryBlue,
            Location = new Point(20, 16),
            Size = new Size(420, 28)
        };

        _lblWarningSelfApproval = new Label
        {
            Text = "⚠️ Self-Approval Violation: You cannot approve your own request.",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.DangerRed,
            BackColor = UiTheme.DangerBg,
            Location = new Point(20, 48),
            Size = new Size(420, 28),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };

        int curY = 82;
        Label CreateDetailField(string caption)
        {
            var captionLbl = new Label { Text = caption, Font = UiTheme.FontSmall, ForeColor = UiTheme.TextSecondary, Location = new Point(20, curY), AutoSize = true };
            var valLbl = new Label { Text = "—", Font = UiTheme.FontBodyBold, ForeColor = UiTheme.TextPrimary, Location = new Point(20, curY + 18), Size = new Size(420, 20) };
            pnlDecision.Controls.Add(captionLbl);
            pnlDecision.Controls.Add(valLbl);
            curY += 44;
            return valLbl;
        }

        _lblRequesterInfo = CreateDetailField("Requester Identity:");
        _lblResourceInfo = CreateDetailField("Requested Infrastructure / Resource:");
        _lblRequestedAtInfo = CreateDetailField("Submission Timestamp (UTC):");
        _lblWindowInfo = CreateDetailField("Requested Access Window:");

        var lblReasonHeader = new Label { Text = "Business Justification:", Font = UiTheme.FontSmall, ForeColor = UiTheme.TextSecondary, Location = new Point(20, curY), AutoSize = true };
        pnlDecision.Controls.Add(lblReasonHeader);
        curY += 20;

        _txtReason = new TextBox
        {
            Location = new Point(20, curY),
            Size = new Size(420, 60),
            Multiline = true,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = UiTheme.FontBody
        };
        pnlDecision.Controls.Add(_txtReason);
        curY += 70;

        var lblCommentHeader = new Label { Text = "Adjudication Comment / Justification Note:", Font = UiTheme.FontSmall, ForeColor = UiTheme.TextSecondary, Location = new Point(20, curY), AutoSize = true };
        pnlDecision.Controls.Add(lblCommentHeader);
        curY += 20;

        _txtDecisionComment = new TextBox
        {
            Location = new Point(20, curY),
            Size = new Size(420, 50),
            Multiline = true,
            Font = UiTheme.FontBody,
            PlaceholderText = "Enter approval conditions or mandatory rejection reasoning..."
        };
        pnlDecision.Controls.Add(_txtDecisionComment);
        curY += 60;

        var pnlButtons = new Panel
        {
            Location = new Point(20, curY),
            Size = new Size(420, 44)
        };

        _btnApprove = new Button
        {
            Text = "✓ Approve Grant",
            Location = new Point(0, 0),
            Size = new Size(160, 40),
            Enabled = false
        };
        UiTheme.StyleSuccessButton(_btnApprove);
        _btnApprove.Click += async (_, _) => await HandleDecisionAsync(true);

        _btnReject = new Button
        {
            Text = "✗ Reject Request",
            Location = new Point(170, 0),
            Size = new Size(150, 40),
            Enabled = false
        };
        UiTheme.StyleDangerButton(_btnReject);
        _btnReject.Click += async (_, _) => await HandleDecisionAsync(false);

        pnlButtons.Controls.AddRange(new Control[] { _btnApprove, _btnReject });
        pnlDecision.Controls.Add(pnlButtons);

        pnlDecision.Controls.AddRange(new Control[]
        {
            _lblSelectedTitle, _lblWarningSelfApproval
        });

        pnlSplit.Controls.Add(pnlQueue, 0, 0);
        pnlSplit.Controls.Add(pnlDecision, 1, 0);

        Controls.Add(pnlSplit);

        pnlHeader.BringToFront();
        pnlSplit.BringToFront();

        Load += async (_, _) => await LoadPendingRequestsAsync();
        Resize += (_, _) => _btnRefresh.Location = new Point(Width - 160, 16);
    }

    private void ConfigurePendingGrid()
    {
        _gridPending.Columns.Clear();
        _gridPending.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#ID", Name = "Id", FillWeight = 12 });
        _gridPending.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resource", Name = "Resource", FillWeight = 36 });
        _gridPending.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requester", Name = "Requester", FillWeight = 30 });
        _gridPending.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Requested (UTC)", Name = "Date", FillWeight = 22 });
    }

    public async Task LoadPendingRequestsAsync()
    {
        _lblStatus.Text = "Retrieving pending requests...";
        _btnRefresh.Enabled = false;

        try
        {
            var pagedResult = await ApiClient.Instance.GetAccessRequestsAsync(
                status: "Pending",
                pageNumber: 1,
                pageSize: 100);

            _pendingRequests.Clear();
            _pendingRequests.AddRange(pagedResult.Items);

            _gridPending.Rows.Clear();
            foreach (var req in _pendingRequests)
            {
                _gridPending.Rows.Add(
                    req.AccessRequestId,
                    req.ResourceName,
                    req.RequesterFullName,
                    $"{req.RequestedAt:MM-dd HH:mm}"
                );
            }

            _lblStatus.Text = $"{_pendingRequests.Count} request(s) awaiting review.";
            if (_pendingRequests.Count > 0)
            {
                _gridPending.Rows[0].Selected = true;
                OnRequestSelected();
            }
            else
            {
                ClearDecisionPanel();
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    private void ClearDecisionPanel()
    {
        _lblSelectedTitle.Text = "No pending requests in queue.";
        _lblRequesterInfo.Text = "—";
        _lblResourceInfo.Text = "—";
        _lblRequestedAtInfo.Text = "—";
        _lblWindowInfo.Text = "—";
        _txtReason.Text = "";
        _txtDecisionComment.Text = "";
        _lblWarningSelfApproval.Visible = false;
        _btnApprove.Enabled = false;
        _btnReject.Enabled = false;
    }

    private AccessRequestDto? GetSelectedRequest()
    {
        if (_gridPending.SelectedRows.Count == 0) return null;
        var cellValue = _gridPending.SelectedRows[0].Cells["Id"].Value;
        if (cellValue is not int selectedId) return null;
        return _pendingRequests.FirstOrDefault(r => r.AccessRequestId == selectedId);
    }

    private void OnRequestSelected()
    {
        var req = GetSelectedRequest();
        if (req == null)
        {
            ClearDecisionPanel();
            return;
        }

        _lblSelectedTitle.Text = $"Review Request #{req.AccessRequestId} — {req.ResourceName}";
        _lblRequesterInfo.Text = $"{req.RequesterFullName} ({req.RequesterUsername})";
        _lblResourceInfo.Text = req.ResourceName;
        _lblRequestedAtInfo.Text = $"{req.RequestedAt:yyyy-MM-dd HH:mm:ss} UTC";

        var windowStr = req.StartAt.HasValue && req.ExpiresAt.HasValue
            ? $"{req.StartAt:MM-dd HH:mm} to {req.ExpiresAt:MM-dd HH:mm} UTC"
            : "Standard 4-hour window from approval timestamp";
        _lblWindowInfo.Text = windowStr;

        _txtReason.Text = req.Reason;
        _txtDecisionComment.Text = "";

        // Self-Approval check: Is current user the requester?
        var isSelf = UserSession.Instance.CurrentUser != null &&
                     UserSession.Instance.CurrentUser.UserId == req.RequesterUserId;

        _lblWarningSelfApproval.Visible = isSelf;
        _btnApprove.Enabled = !isSelf;
        _btnReject.Enabled = true; // Still allow rejecting own request
    }

    private async Task HandleDecisionAsync(bool approve)
    {
        var req = GetSelectedRequest();
        if (req == null) return;

        var comment = _txtDecisionComment.Text.Trim();

        if (!approve && string.IsNullOrWhiteSpace(comment))
        {
            MessageBox.Show("A rejection reason comment is required by policy.", "Comment Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtDecisionComment.Focus();
            return;
        }

        var actionWord = approve ? "APPROVE" : "REJECT";
        var confirm = MessageBox.Show(
            $"Confirm {actionWord} decision for Request #{req.AccessRequestId} ({req.ResourceName})?",
            "Confirm Adjudication",
            MessageBoxButtons.YesNo,
            approve ? MessageBoxIcon.Question : MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _btnApprove.Enabled = false;
        _btnReject.Enabled = false;

        try
        {
            if (approve)
            {
                await ApiClient.Instance.ApproveAccessRequestAsync(req.AccessRequestId, new ApproveAccessRequestDto
                {
                    Comment = string.IsNullOrWhiteSpace(comment) ? "Approved via PAM Desktop Client." : comment
                });
            }
            else
            {
                await ApiClient.Instance.RejectAccessRequestAsync(req.AccessRequestId, new RejectAccessRequestDto
                {
                    Comment = comment
                });
            }

            MessageBox.Show($"Request #{req.AccessRequestId} was successfully {(approve ? "approved" : "rejected")}.",
                "Decision Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information);

            await LoadPendingRequestsAsync();
        }
        catch (ApiException ex)
        {
            MessageBox.Show(ex.Message, "Adjudication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnApprove.Enabled = true;
            _btnReject.Enabled = true;
        }
    }
}
