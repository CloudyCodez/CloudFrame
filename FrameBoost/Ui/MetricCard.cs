using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal sealed class MetricCard : CardPanel
{
    private readonly Label _titleLabel;
    private readonly Label _valueLabel;
    private readonly MarqueeLabel _detailLabel;
    private Color _accentColor = AppTheme.TextPrimary;
    private Color _currentAccent;
    private float _accentAnim;
    private readonly System.Windows.Forms.Timer _accentTimer;

    public MetricCard()
    {
        FillColor = AppTheme.Surface;
        BorderColor = AppTheme.Border;
        BackColor = AppTheme.Canvas;
        Margin = new Padding(0, 0, 16, 0);
        MinimumSize = new Size(220, 110);
        _currentAccent = AppTheme.TextPrimary;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = FillColor,
            Padding = new Padding(6, 0, 0, 0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _titleLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.CaptionFont(8.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2)
        };
        _valueLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(17f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 6, 0, 4),
            BackColor = Color.Transparent
        };
        _detailLabel = new MarqueeLabel
        {
            Font = AppTheme.BodyFont(9f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            Height = 22
        };

        layout.Controls.Add(_titleLabel, 0, 0);
        layout.Controls.Add(_valueLabel, 0, 1);
        layout.Controls.Add(_detailLabel, 0, 2);
        Controls.Add(layout);

        _accentTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _accentTimer.Tick += (_, _) =>
        {
            _accentAnim = Math.Min(1f, _accentAnim + 0.06f);
            _valueLabel.ForeColor = LerpColor(_currentAccent, _accentColor, _accentAnim);
            if (_accentAnim >= 1f)
            {
                _currentAccent = _accentColor;
                _accentTimer.Stop();
            }
            Invalidate();
        };
    }

    public void SetContent(string title, string value, string detail, Color? accent = null)
    {
        _titleLabel.Text = title;
        _valueLabel.Text = value;
        _detailLabel.Text = detail;

        var newAccent = accent ?? AppTheme.TextPrimary;
        if (newAccent != _accentColor)
        {
            _currentAccent = _valueLabel.ForeColor;
            _accentColor = newAccent;
            _accentAnim = 0f;
            _accentTimer.Start();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Accent bar on left edge
        var barHeight = (int)(Height * 0.55f);
        var barY = (Height - barHeight) / 2;
        var barRect = new Rectangle(0, barY, 3, barHeight);
        using var barPath = AppTheme.CreateRoundedRectangle(barRect, 2);
        using var barBrush = new LinearGradientBrush(
            barRect.IsEmpty ? new Rectangle(0, 0, 3, barHeight) : barRect,
            _valueLabel.ForeColor,
            Color.FromArgb(60, _valueLabel.ForeColor),
            LinearGradientMode.Vertical);
        e.Graphics.FillPath(barBrush, barPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _accentTimer.Dispose();
        base.Dispose(disposing);
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
