using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class PerformanceOverlayForm : Form
{
    private const int EdgeMargin = 18;
    private const int HorizontalPadding = 14;
    private const int VerticalPadding = 11;
    private const float CardCornerRadius = 11f;

    private static readonly Color ShadowColor = Color.FromArgb(150, 0, 0, 0);
    private static readonly Color WarningColor = Color.FromArgb(255, 251, 146, 60);

    private readonly AppSettings _settings;
    private readonly string _pinnedScreen;

    private TelemetrySnapshot _snapshot = new();
    private Size _lastComputedSize;

    public PerformanceOverlayForm(AppSettings settings)
    {
        _settings = settings;
        _pinnedScreen = (Screen.PrimaryScreen ?? Screen.FromPoint(Location)).DeviceName;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        ApplySize();
        Location = ComputeLocation();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020   // WS_EX_TRANSPARENT
                        | 0x00080000   // WS_EX_LAYERED
                        | 0x00000080   // WS_EX_TOOLWINDOW
                        | 0x08000000;  // WS_EX_NOACTIVATE
            return cp;
        }
    }

    public void UpdateSnapshot(TelemetrySnapshot snapshot)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateSnapshot(snapshot));
            return;
        }

        _snapshot = snapshot;
        ApplySize();
        EnsurePinnedLocation();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        if (_settings.OverlayStyle == OverlayStyle.Minimal)
        {
            PaintMinimal(graphics);
        }
        else
        {
            PaintCard(graphics);
        }
    }

    private void ApplySize()
    {
        var size = ComputeSize();
        if (size == _lastComputedSize)
        {
            return;
        }

        _lastComputedSize = size;
        Size = size;
        Location = ComputeLocation();
    }

    private Size ComputeSize()
    {
        using var titleFont = CreateOverlayFont(8f, FontStyle.Bold);
        using var fpsFont = CreateOverlayFont(28f, FontStyle.Bold);
        using var statusFont = CreateOverlayFont(8.5f, FontStyle.Regular);
        using var statsFont = CreateOverlayFont(9f, FontStyle.Bold);

        var width = Math.Max(MeasureWidth("CloudFrame Live", titleFont), MeasureWidth(GetStatusText(), statusFont));

        if (_settings.OverlayShowFps)
        {
            width = Math.Max(width, MeasureWidth("240 FPS", fpsFont));
        }

        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            width = Math.Max(width, MeasureWidth("CPU 100%   GPU 100%", statsFont));
        }

        width = Math.Clamp(width + HorizontalPadding * 2 + 8, 180, 320);

        var height = VerticalPadding + 16;
        if (_settings.OverlayShowFps)
        {
            height += 36;
        }

        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            height += 18;
        }

        height += 18;
        height += VerticalPadding;

        if (_settings.OverlayStyle == OverlayStyle.Card)
        {
            return new Size(Math.Max(220, width), Math.Max(96, height));
        }

        return new Size(Math.Max(192, width), Math.Max(82, height));
    }

    private void PaintMinimal(Graphics graphics)
    {
        var accent = GetAccentColor();
        var text = GetTextColor();
        var mutedText = WithAlpha(text, 195);

        var x = 4f;
        var y = (float)VerticalPadding;

        using var titleFont = CreateOverlayFont(8f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(mutedText);
        DrawShadowed(graphics, "CloudFrame Live", titleFont, titleBrush, x, y);
        y += 18f;

        if (_settings.OverlayShowFps)
        {
            var fpsText = GetFpsDisplayText();
            var hasFps = _snapshot.FramesPerSecond is > 0;
            using var fpsFont = CreateOverlayFont(28f, FontStyle.Bold);
            using var unitFont = CreateOverlayFont(9f, FontStyle.Bold);
            using var fpsBrush = new SolidBrush(hasFps && _snapshot.FramesPerSecond < 30 ? WarningColor : accent);
            using var unitBrush = new SolidBrush(mutedText);

            DrawShadowed(graphics, fpsText, fpsFont, fpsBrush, x - 1f, y);
            var valueWidth = MeasureWidth(fpsText, fpsFont);
            DrawShadowed(graphics, "FPS", unitFont, unitBrush, x + valueWidth + 1f, y + 16f);
            y += 38f;
        }

        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            using var statsFont = CreateOverlayFont(9f, FontStyle.Bold);
            using var statsBrush = new SolidBrush(accent);
            DrawShadowed(graphics, GetStatsText(), statsFont, statsBrush, x, y);
            y += 17f;
        }

        using var statusFont = CreateOverlayFont(8.5f, FontStyle.Regular);
        using var statusBrush = new SolidBrush(text);
        DrawShadowed(graphics, GetStatusText(), statusFont, statusBrush, x, y);
    }

    private void PaintCard(Graphics graphics)
    {
        var accent = GetAccentColor();
        var text = GetTextColor();
        var background = GetBackgroundColor();
        var mutedText = WithAlpha(text, 185);
        var border = WithAlpha(text, 48);

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var cardPath = CreateRoundedRectangle(bounds, CardCornerRadius);

        using (var backgroundBrush = new LinearGradientBrush(
                   new PointF(bounds.Left, bounds.Top),
                   new PointF(bounds.Right, bounds.Bottom),
                   background,
                   Blend(background, accent, 0.14f)))
        {
            graphics.FillPath(backgroundBrush, cardPath);
        }

        using (var glowBrush = new LinearGradientBrush(
                   new PointF(bounds.Left + CardCornerRadius, bounds.Top),
                   new PointF(bounds.Right - CardCornerRadius, bounds.Top),
                   Color.Transparent,
                   Color.Transparent))
        {
            glowBrush.InterpolationColors = new ColorBlend
            {
                Positions = [0f, 0.2f, 0.8f, 1f],
                Colors =
                [
                    Color.Transparent,
                    WithAlpha(accent, 90),
                    WithAlpha(accent, 90),
                    Color.Transparent
                ]
            };

            graphics.FillRectangle(
                glowBrush,
                bounds.Left + CardCornerRadius,
                bounds.Top,
                bounds.Width - CardCornerRadius * 2,
                2f);
        }

        using (var leftRail = new LinearGradientBrush(
                   new PointF(bounds.Left, bounds.Top + CardCornerRadius),
                   new PointF(bounds.Left, bounds.Bottom - CardCornerRadius),
                   accent,
                   WithAlpha(accent, 30)))
        {
            graphics.FillRectangle(leftRail, bounds.Left + 1f, bounds.Top + CardCornerRadius, 3f, bounds.Height - CardCornerRadius * 2);
        }

        using var borderPen = new Pen(border, 0.9f);
        graphics.DrawPath(borderPen, cardPath);

        var x = bounds.Left + HorizontalPadding + 8f;
        var y = bounds.Top + VerticalPadding;

        using var titleFont = CreateOverlayFont(8f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(mutedText);
        graphics.DrawString("CloudFrame Live", titleFont, titleBrush, new PointF(x, y));
        y += 18f;

        if (_settings.OverlayShowFps)
        {
            var fpsText = GetFpsDisplayText();
            var hasFps = _snapshot.FramesPerSecond is > 0;
            using var fpsFont = CreateOverlayFont(28f, FontStyle.Bold);
            using var unitFont = CreateOverlayFont(9f, FontStyle.Bold);
            using var fpsBrush = new SolidBrush(hasFps && _snapshot.FramesPerSecond < 30 ? WarningColor : accent);
            using var unitBrush = new SolidBrush(mutedText);

            graphics.DrawString(fpsText, fpsFont, fpsBrush, new PointF(x - 1f, y));
            var valueWidth = MeasureWidth(fpsText, fpsFont);
            graphics.DrawString("FPS", unitFont, unitBrush, new PointF(x + valueWidth + 2f, y + 16f));
            y += 38f;
        }

        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            using var statsFont = CreateOverlayFont(9f, FontStyle.Bold);
            using var statsBrush = new SolidBrush(accent);
            graphics.DrawString(GetStatsText(), statsFont, statsBrush, new PointF(x, y));
            y += 17f;
        }

        using var statusFont = CreateOverlayFont(8.5f, FontStyle.Regular);
        using var statusBrush = new SolidBrush(text);
        graphics.DrawString(GetStatusText(), statusFont, statusBrush, new PointF(x, y));
    }

    private string GetFpsDisplayText()
    {
        return _snapshot.FramesPerSecond is > 0
            ? $"{_snapshot.FramesPerSecond:0}"
            : "--";
    }

    private string GetStatsText()
    {
        var parts = new List<string>();
        if (_settings.OverlayShowCpu)
        {
            parts.Add($"CPU {_snapshot.CpuPercent:0}%");
        }

        if (_settings.OverlayShowGpu)
        {
            var gpuText = _snapshot.GpuPercent is double gpu ? $"{gpu:0}%" : "--%";
            parts.Add($"GPU {gpuText}");
        }

        return string.Join("   ", parts);
    }

    private string GetStatusText()
    {
        return string.IsNullOrWhiteSpace(_snapshot.FpsStatus)
            ? "Waiting for boost session"
            : _snapshot.FpsStatus;
    }

    private Color GetAccentColor() => Color.FromArgb(_settings.OverlayAccentArgb);

    private Color GetTextColor() => Color.FromArgb(_settings.OverlayTextArgb);

    private Color GetBackgroundColor() => Color.FromArgb(_settings.OverlayBackgroundArgb);

    private Font CreateOverlayFont(float size, FontStyle style)
    {
        try
        {
            return new Font(GetOverlayFontFamily(), size, style, GraphicsUnit.Point);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }
    }

    private string GetOverlayFontFamily()
    {
        return _settings.OverlayFontPreset switch
        {
            OverlayFontPreset.Segoe => "Segoe UI",
            OverlayFontPreset.Consolas => "Consolas",
            OverlayFontPreset.Trebuchet => "Trebuchet MS",
            _ => "Bahnschrift"
        };
    }

    private static int MeasureWidth(string text, Font font)
    {
        return TextRenderer.MeasureText(
            text,
            font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding).Width;
    }

    private static void DrawShadowed(Graphics graphics, string text, Font font, Brush brush, float x, float y)
    {
        using var shadowBrush = new SolidBrush(ShadowColor);
        graphics.DrawString(text, font, shadowBrush, new PointF(x + 1.4f, y + 1.4f));
        graphics.DrawString(text, font, brush, new PointF(x, y));
    }

    private static Color WithAlpha(Color color, int alpha)
    {
        return Color.FromArgb(Math.Clamp(alpha, 0, 255), color.R, color.G, color.B);
    }

    private static Color Blend(Color baseColor, Color accentColor, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var alpha = Math.Clamp((int)(baseColor.A + ((255 - baseColor.A) * amount)), 0, 255);
        var red = (int)(baseColor.R + ((accentColor.R - baseColor.R) * amount));
        var green = (int)(baseColor.G + ((accentColor.G - baseColor.G) * amount));
        var blue = (int)(baseColor.B + ((accentColor.B - baseColor.B) * amount));
        return Color.FromArgb(alpha, red, green, blue);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Point ComputeLocation()
    {
        try
        {
            var screen = Screen.AllScreens
                .FirstOrDefault(candidate => string.Equals(candidate.DeviceName, _pinnedScreen, StringComparison.OrdinalIgnoreCase))
                ?? Screen.PrimaryScreen
                ?? Screen.FromPoint(Location);

            var workingArea = screen.WorkingArea;
            return _settings.OverlayPosition switch
            {
                OverlayPosition.TopRight => new Point(workingArea.Right - Width - EdgeMargin, workingArea.Top + EdgeMargin),
                OverlayPosition.BottomLeft => new Point(workingArea.Left + EdgeMargin, workingArea.Bottom - Height - EdgeMargin),
                OverlayPosition.BottomRight => new Point(workingArea.Right - Width - EdgeMargin, workingArea.Bottom - Height - EdgeMargin),
                _ => new Point(workingArea.Left + EdgeMargin, workingArea.Top + EdgeMargin)
            };
        }
        catch
        {
            return new Point(EdgeMargin, EdgeMargin);
        }
    }

    private void EnsurePinnedLocation()
    {
        var target = ComputeLocation();
        if (Location != target)
        {
            Location = target;
        }
    }
}
