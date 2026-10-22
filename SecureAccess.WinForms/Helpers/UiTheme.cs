namespace SecureAccess.WinForms.Helpers;

public static class UiTheme
{
    // =========================================================================
    // Color Palette
    // =========================================================================

    // Dark Navy Sidebar
    public static readonly Color SidebarBackground = Color.FromArgb(17, 24, 39);      // Slate 900
    public static readonly Color SidebarHeader = Color.FromArgb(15, 23, 42);          // Slate 950
    public static readonly Color SidebarItemHover = Color.FromArgb(30, 41, 59);        // Slate 800
    public static readonly Color SidebarItemSelected = Color.FromArgb(37, 99, 235);     // Blue 600
    public static readonly Color SidebarText = Color.FromArgb(226, 232, 240);          // Slate 200
    public static readonly Color SidebarTextMuted = Color.FromArgb(148, 163, 184);     // Slate 400

    // Content Canvas
    public static readonly Color ContentBackground = Color.FromArgb(248, 250, 252);   // Slate 50
    public static readonly Color CardBackground = Color.White;
    public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);          // Slate 200
    public static readonly Color BorderColorFocus = Color.FromArgb(59, 130, 246);     // Blue 500

    // Typography Colors
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);             // Slate 900
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);        // Slate 500
    public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);            // Slate 400

    // Brand & Status Accents
    public static readonly Color PrimaryBlue = Color.FromArgb(37, 99, 235);            // Blue 600
    public static readonly Color PrimaryBlueHover = Color.FromArgb(29, 78, 216);       // Blue 700

    public static readonly Color SuccessGreen = Color.FromArgb(22, 163, 74);           // Green 600
    public static readonly Color SuccessBg = Color.FromArgb(220, 252, 231);              // Green 100

    public static readonly Color WarningAmber = Color.FromArgb(217, 119, 6);            // Amber 600
    public static readonly Color WarningBg = Color.FromArgb(254, 243, 199);             // Amber 100

    public static readonly Color DangerRed = Color.FromArgb(220, 38, 38);               // Red 600
    public static readonly Color DangerBg = Color.FromArgb(254, 226, 226);              // Red 100

    public static readonly Color RevokedGray = Color.FromArgb(75, 85, 99);              // Gray 600
    public static readonly Color RevokedBg = Color.FromArgb(243, 244, 246);             // Gray 100

    // =========================================================================
    // Typography Fonts
    // =========================================================================

    public static readonly Font FontTitle = new("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font FontSection = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font FontSubheader = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font FontBody = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font FontBodyBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font FontBadge = new("Segoe UI", 8.5f, FontStyle.Bold);

    // =========================================================================
    // Styling Helpers
    // =========================================================================

    public static void ApplyModernGridStyle(DataGridView grid)
    {
        grid.BackgroundColor = CardBackground;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = BorderColor;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.Font = FontBody;

        // Column Headers
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249); // Slate 100
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = FontSubheader;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 10, 12, 10);
        grid.ColumnHeadersHeight = 44;

        // Rows
        grid.RowsDefaultCellStyle.BackColor = CardBackground;
        grid.RowsDefaultCellStyle.ForeColor = TextPrimary;
        grid.RowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255); // Blue 50
        grid.RowsDefaultCellStyle.SelectionForeColor = PrimaryBlue;
        grid.RowsDefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
        grid.RowTemplate.Height = 44;

        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); // Slate 50
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = PrimaryBlue;
    }

    public static void StylePrimaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = PrimaryBlue;
        btn.ForeColor = Color.White;
        btn.Font = FontBodyBold;
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(14, 0, 14, 0);

        btn.MouseEnter += (_, _) => btn.BackColor = PrimaryBlueHover;
        btn.MouseLeave += (_, _) => btn.BackColor = PrimaryBlue;
    }

    public static void StyleSecondaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = BorderColor;
        btn.FlatAppearance.BorderSize = 1;
        btn.BackColor = Color.White;
        btn.ForeColor = TextPrimary;
        btn.Font = FontBodyBold;
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(14, 0, 14, 0);

        btn.MouseEnter += (_, _) => btn.BackColor = Color.FromArgb(241, 245, 249);
        btn.MouseLeave += (_, _) => btn.BackColor = Color.White;
    }

    public static void StyleSuccessButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = SuccessGreen;
        btn.ForeColor = Color.White;
        btn.Font = FontBodyBold;
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(14, 0, 14, 0);

        btn.MouseEnter += (_, _) => btn.BackColor = Color.FromArgb(21, 128, 61);
        btn.MouseLeave += (_, _) => btn.BackColor = SuccessGreen;
    }

    public static void StyleDangerButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = DangerRed;
        btn.ForeColor = Color.White;
        btn.Font = FontBodyBold;
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(14, 0, 14, 0);

        btn.MouseEnter += (_, _) => btn.BackColor = Color.FromArgb(185, 28, 28);
        btn.MouseLeave += (_, _) => btn.BackColor = DangerRed;
    }

    public static (Color TextColor, Color BgColor) GetStatusColors(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "approved" or "active" => (SuccessGreen, SuccessBg),
            "pending" => (WarningAmber, WarningBg),
            "rejected" or "inactive" or "denied" => (DangerRed, DangerBg),
            "revoked" => (RevokedGray, RevokedBg),
            _ => (TextSecondary, Color.FromArgb(241, 245, 249))
        };
    }
}
