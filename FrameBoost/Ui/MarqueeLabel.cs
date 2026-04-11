using System.ComponentModel;

namespace FrameBoost.Ui;

internal sealed class MarqueeLabel : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolTip _toolTip;
    private int _textOffset;
    private int _pauseTicks;
    private bool _hovering;

    public MarqueeLabel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);

        AutoSize = false;
        Height = 20;
        BackColor = Color.Transparent;
        ForeColor = AppTheme.TextSecondary;
        Font = AppTheme.BodyFont(9.5f);

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 35
        };
        _timer.Tick += (_, _) => AdvanceScroll();

        _toolTip = new ToolTip
        {
            InitialDelay = 150,
            ReshowDelay = 80,
            AutoPopDelay = 10000,
            ShowAlways = true
        };

        MouseEnter += (_, _) =>
        {
            _hovering = true;
            UpdateToolTip();
        };
        MouseLeave += (_, _) =>
        {
            _hovering = false;
            _toolTip.Hide(this);
        };
        SizeChanged += (_, _) => ResetScroll();
        FontChanged += (_, _) => ResetScroll();
        TextChanged += (_, _) => ResetScroll();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var backgroundBrush = new SolidBrush(ResolveBackgroundColor());
        e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

        var text = Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        var textSize = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, Height), flags);
        var overflow = Math.Max(0, textSize.Width - ClientSize.Width);

        if (overflow <= 0)
        {
            TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, ForeColor, flags);
            return;
        }

        var drawY = Math.Max(0, (Height - textSize.Height) / 2);
        TextRenderer.DrawText(e.Graphics, text, Font, new Point(-_textOffset, drawY), ForeColor, flags);

        const int gap = 36;
        TextRenderer.DrawText(e.Graphics, text, Font, new Point(textSize.Width + gap - _textOffset, drawY), ForeColor, flags);
    }

    private Color ResolveBackgroundColor()
    {
        if (BackColor != Color.Transparent)
        {
            return BackColor;
        }

        var current = Parent;
        while (current is not null)
        {
            if (current.BackColor != Color.Transparent)
            {
                return current.BackColor;
            }

            current = current.Parent;
        }

        return AppTheme.Surface;
    }

    private void AdvanceScroll()
    {
        var textWidth = TextRenderer.MeasureText(Text ?? string.Empty, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding).Width;
        var overflow = Math.Max(0, textWidth - ClientSize.Width);
        if (overflow <= 0)
        {
            _timer.Stop();
            _textOffset = 0;
            Invalidate();
            return;
        }

        if (_pauseTicks > 0)
        {
            _pauseTicks--;
            return;
        }

        _textOffset++;
        const int gap = 36;
        if (_textOffset > textWidth + gap)
        {
            _textOffset = 0;
            _pauseTicks = 18;
        }

        Invalidate();
    }

    private void ResetScroll()
    {
        _textOffset = 0;
        _pauseTicks = 18;

        var textWidth = TextRenderer.MeasureText(Text ?? string.Empty, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding).Width;
        if (textWidth > ClientSize.Width)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        UpdateToolTip();
        Invalidate();
    }

    private void UpdateToolTip()
    {
        var text = Text ?? string.Empty;
        _toolTip.SetToolTip(this, string.IsNullOrWhiteSpace(text) ? null : text);

        if (_hovering && !string.IsNullOrWhiteSpace(text))
        {
            _toolTip.Show(text, this, Width / 2, Height + 8, 5000);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
