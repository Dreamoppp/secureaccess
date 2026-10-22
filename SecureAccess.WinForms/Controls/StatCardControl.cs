using SecureAccess.WinForms.Helpers;

namespace SecureAccess.WinForms.Controls;

public class StatCardControl : Panel
{
    private readonly Label _lblTitle;
    private readonly Label _lblValue;
    private readonly Label _lblSubtitle;
    private readonly Panel _accentBar;

    public string CardTitle
    {
        get => _lblTitle.Text;
        set => _lblTitle.Text = value;
    }

    public string Value
    {
        get => _lblValue.Text;
        set => _lblValue.Text = value;
    }

    public string Subtitle
    {
        get => _lblSubtitle.Text;
        set => _lblSubtitle.Text = value;
    }

    public Color AccentColor
    {
        get => _accentBar.BackColor;
        set => _accentBar.BackColor = value;
    }

    public StatCardControl()
    {
        BackColor = UiTheme.CardBackground;
        BorderStyle = BorderStyle.FixedSingle;
        Size = new Size(220, 110);
        Padding = new Padding(16, 12, 16, 12);

        _accentBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = UiTheme.PrimaryBlue
        };

        _lblTitle = new Label
        {
            Text = "Metric Title",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(16, 14),
            AutoSize = true
        };

        _lblValue = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(14, 34),
            AutoSize = true
        };

        _lblSubtitle = new Label
        {
            Text = "Context information",
            Font = UiTheme.FontSmall,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(16, 78),
            AutoSize = true
        };

        Controls.Add(_lblSubtitle);
        Controls.Add(_lblValue);
        Controls.Add(_lblTitle);
        Controls.Add(_accentBar);
    }
}
