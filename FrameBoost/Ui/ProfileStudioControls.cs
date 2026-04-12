using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal sealed class BannerPanel : Panel
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 90 };
    private float _phase;
    private bool _animationEnabled = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = AppTheme.AccentStrong;

    public BannerPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        _timer.Tick += (_, _) =>
        {
            _phase += 0.008f;
            if (_phase > MathF.PI * 2f)
            {
                _phase -= MathF.PI * 2f;
            }

            Invalidate();
        };
        UpdateAnimationState();
    }

    public void SetAnimationEnabled(bool enabled)
    {
        _animationEnabled = enabled;
        UpdateAnimationState();
        if (!enabled)
        {
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimationState();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 28);
        using var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(28, 40, 68), Color.FromArgb(14, 82, 100), 165f);
        g.FillPath(gradient, path);

        using var tintPath = new GraphicsPath();
        tintPath.AddEllipse(Width - 120, -60, 220, 220);
        using var tintBrush = new PathGradientBrush(tintPath)
        {
            CenterColor = Color.FromArgb(34, AccentColor),
            SurroundColors = [Color.FromArgb(0, AccentColor)]
        };
        g.FillPath(tintBrush, tintPath);

        DrawWave(g, Height * 0.48f, 12f, 52, 0f);
        DrawWave(g, Height * 0.62f, 8f, 28, 1.4f);

        for (var i = 0; i < 10; i++)
        {
            var x = 24 + (i * 103 % Math.Max(Width - 48, 60));
            var y = 16 + (i * 61 % Math.Max(Height - 32, 40));
            using var dotBrush = new SolidBrush(Color.FromArgb(18 + (i % 3) * 10, 230, 240, 255));
            g.FillEllipse(dotBrush, x, y, 2f, 2f);
        }

        using var borderPen = new Pen(Color.FromArgb(72, AccentColor), 1f);
        g.DrawPath(borderPen, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawWave(Graphics g, float midY, float amplitude, int alpha, float phaseOffset)
    {
        const int segments = 24;
        var points = new PointF[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var x = t * Width;
            var y = midY + MathF.Sin(_phase + phaseOffset + t * MathF.PI * 3.2f) * amplitude;
            points[i] = new PointF(x, y);
        }

        using var pen = new Pen(Color.FromArgb(alpha, AccentColor), 1.35f);
        g.DrawCurve(pen, points, 0.35f);
    }

    private void UpdateAnimationState()
    {
        if (_animationEnabled && Visible)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }
}

internal sealed class AvatarBadge : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private float _hover;
    private bool _hovered;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = AppTheme.AccentStrong;

    public AvatarBadge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(104, 104);
        Cursor = Cursors.Hand;
        _timer.Tick += (_, _) =>
        {
            var target = _hovered ? 1f : 0f;
            _hover += (target - _hover) * 0.18f;
            if (Math.Abs(_hover - target) < 0.01f)
            {
                _hover = target;
                if (!_hovered)
                {
                    _timer.Stop();
                }
            }

            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        _timer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var outer = new Rectangle(2, 2, Width - 5, Height - 5);
        using var glowBrush = new SolidBrush(Color.FromArgb((int)(72 + (_hover * 52)), AccentColor));
        g.FillEllipse(glowBrush, outer);

        var inner = Rectangle.Inflate(outer, -9, -9);
        using var innerBrush = new SolidBrush(Color.FromArgb(22, 28, 38));
        g.FillEllipse(innerBrush, inner);

        if (Image is not null)
        {
            g.DrawImage(Image, Rectangle.Inflate(inner, -8, -8));
        }

        using var ringPen = new Pen(Color.FromArgb((int)(175 + (_hover * 35)), Color.White), 2f);
        g.DrawEllipse(ringPen, inner);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class StatusPill : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = AppTheme.AccentStrong;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PulseEnabled { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Pulse { get; set; } = 0.65f;

    public StatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 28;
        Margin = new Padding(0, 0, 8, 8);
        Font = AppTheme.CaptionFont(9f);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        RemeasureWidth();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RemeasureWidth();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 14);
        using var fillBrush = new SolidBrush(Color.FromArgb(46, 24, 31, 48));
        g.FillPath(fillBrush, path);

        var dotAlpha = PulseEnabled ? (int)(100 + (120 * Pulse)) : 120;
        using var dotBrush = new SolidBrush(Color.FromArgb(dotAlpha, AccentColor));
        g.FillEllipse(dotBrush, 10, 9, 8, 8);

        TextRenderer.DrawText(g, Text, Font, new Rectangle(24, 6, Width - 30, Height - 8), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void RemeasureWidth()
    {
        if (string.IsNullOrEmpty(Text))
        {
            Width = 94;
            return;
        }

        using var g = CreateGraphics();
        var measured = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Width = Math.Max(72, measured.Width + 40);
    }
}

internal sealed class SectionCard : CardPanel
{
    private readonly Button _toggleButton;
    private readonly TableLayoutPanel _content;
    private bool _expanded = true;

    public SectionCard(string title, string subtitle)
    {
        FillColor = AppTheme.Surface;
        BorderColor = AppTheme.Border;
        CornerRadius = 18;
        EnableSpotlight = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, BackColor = Color.Transparent };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var heading = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
        heading.Controls.Add(new Label { Text = title, AutoSize = true, Font = AppTheme.TitleFont(12.5f), ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent });
        heading.Controls.Add(new Label { Text = subtitle, AutoSize = true, MaximumSize = new Size(540, 0), Font = AppTheme.BodyFont(9.4f), ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 0) });

        _toggleButton = AppTheme.CreateButton("Collapse", width: 96);
        _toggleButton.Click += (_, _) => SetExpanded(!_expanded);

        _content = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0, 14, 0, 0) };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(_toggleButton, 1, 0);
        layout.Controls.Add(_content, 0, 1);
        layout.SetColumnSpan(_content, 2);
        Controls.Add(layout);
    }

    public void AddDetailRow(string label, Control valueControl)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label, AutoSize = true, Font = AppTheme.CaptionFont(9.1f), ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent, Margin = new Padding(0, 0, 14, 0) }, 0, 0);
        row.Controls.Add(valueControl, 1, 0);
        _content.Controls.Add(row);
    }

    public void AddInlineEditor(string label, Control editor)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
        row.Controls.Add(new Label { Text = $"{label}:", AutoSize = true, Font = AppTheme.CaptionFont(9.1f), ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 0) });
        row.Controls.Add(editor);
        _content.Controls.Add(row);
    }

    public void AddControl(Control control)
        => _content.Controls.Add(control);

    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _content.Visible = expanded;
        _toggleButton.Text = expanded ? "Collapse" : "Expand";
    }
}
