using System.ComponentModel;
using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class BoostPresetCard : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 33 };
    private float _hoverProgress;
    private bool _isHovered;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BoostPresetOption Preset { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? PresetTitle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Description { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ImpactScore { get; set; } = 65;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(13, 148, 136);

    public event EventHandler? CardSelected;

    public BoostPresetCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);

        Size = new Size(220, 176);
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 16, 14);

        _animationTimer.Tick += (_, _) =>
        {
            var target = _isHovered ? 1f : 0f;
            _hoverProgress += (target - _hoverProgress) * 0.22f;
            if (Math.Abs(_hoverProgress - target) < 0.02f)
            {
                _hoverProgress = target;
                if (!_isHovered)
                {
                    _animationTimer.Stop();
                }
            }

            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        _animationTimer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _animationTimer.Start();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        CardSelected?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = AppTheme.CreateRoundedRectangle(rect, 18);

        var fill = IsSelected
            ? Blend(Color.FromArgb(42, 46, 53), AccentColor, 0.16f)
            : Blend(Color.FromArgb(42, 46, 53), AccentColor, _hoverProgress * 0.05f);
        using var fillBrush = new SolidBrush(fill);
        g.FillPath(fillBrush, path);

        if (IsSelected || _hoverProgress > 0.01f)
        {
            var glowAlpha = IsSelected ? 34 : (int)(20 * _hoverProgress);
            using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, AccentColor));
            g.FillRectangle(glowBrush, rect.X + 1, rect.Y + 1, 4, rect.Height - 2);
        }

        var borderColor = IsSelected
            ? Color.FromArgb(220, AccentColor)
            : Blend(AppTheme.Border, AccentColor, _hoverProgress * 0.45f);
        using var borderPen = new Pen(borderColor, IsSelected ? 1.6f : 1.2f);
        g.DrawPath(borderPen, path);

        DrawContent(g, rect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawContent(Graphics g, Rectangle rect)
    {
        const int pad = 18;
        var title = PresetTitle ?? Preset.ToString();
        var icon = Preset switch
        {
            BoostPresetOption.Balanced => "⬡",
            BoostPresetOption.Performance => "⚡",
            BoostPresetOption.MaxFps => "▣",
            _ => "◆"
        };

        using var iconFont = AppTheme.TitleFont(18f);
        using var titleFont = AppTheme.TitleFont(11.5f);
        using var bodyFont = AppTheme.BodyFont(9.1f);
        using var captionFont = AppTheme.CaptionFont(8.5f);
        using var accentBrush = new SolidBrush(AccentColor);
        using var titleBrush = new SolidBrush(Color.FromArgb(245, 248, 252));
        using var bodyBrush = new SolidBrush(Color.FromArgb(186, 195, 208));

        g.DrawString(icon, iconFont, accentBrush, rect.X + pad, rect.Y + 10);
        g.DrawString(title, titleFont, titleBrush, rect.X + pad, rect.Y + 42);

        if (IsSelected)
        {
            const string activeText = "ACTIVE";
            var activeSize = g.MeasureString(activeText, captionFont);
            g.DrawString(activeText, captionFont, accentBrush, rect.Right - activeSize.Width - pad, rect.Y + 14);
        }

        var descriptionRect = new RectangleF(rect.X + pad, rect.Y + 70, rect.Width - (pad * 2), 58);
        g.DrawString(Description ?? string.Empty, bodyFont, bodyBrush, descriptionRect);

        var barX = rect.X + pad;
        var barY = rect.Bottom - 22;
        var barWidth = rect.Width - (pad * 2);
        var fillWidth = (int)(barWidth * (ImpactScore / 100f));

        using var trackBrush = new SolidBrush(Color.FromArgb(62, 71, 86));
        g.FillRectangle(trackBrush, barX, barY, barWidth, 5);

        if (fillWidth > 0)
        {
            using var fillBrush = new SolidBrush(AccentColor);
            g.FillRectangle(fillBrush, barX, barY, fillWidth, 5);
        }

        g.DrawString($"{ImpactScore}% scope", captionFont, accentBrush, barX, barY - 16);
    }

    private static Color Blend(Color left, Color right, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(left.A + ((right.A - left.A) * amount)),
            (int)(left.R + ((right.R - left.R) * amount)),
            (int)(left.G + ((right.G - left.G) * amount)),
            (int)(left.B + ((right.B - left.B) * amount)));
    }
}
