using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal sealed class BoostGaugeControl : Control
{
    private int _targetValue;
    private float _displayValue;
    private string _caption = string.Empty;
    private string _detail = string.Empty;
    private readonly System.Windows.Forms.Timer _animTimer;

    public BoostGaugeControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = 108;
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.TextPrimary;

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += (_, _) =>
        {
            var diff = _targetValue - _displayValue;
            if (Math.Abs(diff) < 0.3f)
            {
                _displayValue = _targetValue;
                _animTimer.Stop();
            }
            else
            {
                _displayValue += diff * 0.10f;
            }
            Invalidate();
        };
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _targetValue;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (_targetValue == clamped) return;
            _targetValue = clamped;
            _animTimer.Start();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _caption;
        set { if (_caption != value) { _caption = value ?? string.Empty; Invalidate(); } }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Detail
    {
        get => _detail;
        set { if (_detail != value) { _detail = value ?? string.Empty; Invalidate(); } }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var backBrush = new SolidBrush(ResolveBackgroundColor());
        g.FillRectangle(backBrush, ClientRectangle);

        // Caption
        var captionRect = new Rectangle(14, 8, Width - 112, 20);
        TextRenderer.DrawText(g, _caption, AppTheme.CaptionFont(9.5f), captionRect,
            AppTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

        // Value %
        var valueRect = new Rectangle(Width - 96, 6, 82, 22);
        TextRenderer.DrawText(g, $"{_displayValue:0}%", AppTheme.TitleFont(13f), valueRect,
            ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        // Track
        var barBounds = new Rectangle(14, 34, Width - 28, 16);
        using var trackPath = AppTheme.CreateRoundedRectangle(barBounds, 8);
        using var trackBrush = new SolidBrush(Color.FromArgb(70, 48, 54, 64));
        using var trackBorder = new Pen(Color.FromArgb(100, 80, 88, 100));
        g.FillPath(trackBrush, trackPath);
        g.DrawPath(trackBorder, trackPath);

        // Fill with gradient + glow
        var fillWidth = (int)Math.Round(barBounds.Width * (_displayValue / 100d));
        if (fillWidth > 2)
        {
            var fillBounds = new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height);
            using var fillPath = AppTheme.CreateRoundedRectangle(fillBounds, 8);

            using var fillBrush = new LinearGradientBrush(
                fillBounds,
                AppTheme.AccentStrong,
                AppTheme.Success,
                LinearGradientMode.Horizontal);
            g.FillPath(fillBrush, fillPath);

            // Shine strip on top
            var shineBounds = new Rectangle(fillBounds.X + 4, fillBounds.Y + 2, Math.Max(1, fillBounds.Width - 8), fillBounds.Height / 2 - 1);
            if (shineBounds.Width > 0)
            {
                using var shineGrad = new LinearGradientBrush(shineBounds,
                    Color.FromArgb(55, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                    LinearGradientMode.Vertical);
                using var shinePath = AppTheme.CreateRoundedRectangle(shineBounds, 4);
                g.FillPath(shineGrad, shinePath);
            }
        }

        // Detail text
        if (!string.IsNullOrWhiteSpace(_detail))
        {
            var detailRect = new Rectangle(14, 58, Width - 28, 40);
            TextRenderer.DrawText(g, _detail, AppTheme.BodyFont(8.8f), detailRect,
                AppTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }

    private Color ResolveBackgroundColor()
    {
        var current = Parent;
        while (current is not null)
        {
            if (current.BackColor != Color.Transparent) return current.BackColor;
            current = current.Parent;
        }
        return AppTheme.Surface;
    }
}
