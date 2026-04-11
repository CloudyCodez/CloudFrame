using System.Drawing.Drawing2D;
using FrameBoost.Models;

namespace FrameBoost.Ui;

internal static class AppTheme
{
    private sealed record ThemePalette(
        Color Canvas,
        Color Surface,
        Color SurfaceAlt,
        Color Border,
        Color TextPrimary,
        Color TextSecondary,
        Color Accent,
        Color AccentStrong,
        Color AccentSoft,
        Color Success,
        Color Warning,
        Color Danger);

    private static ThemePalette _current = CreatePalette(AppThemePreset.Graphite);

    public static Color Canvas => _current.Canvas;
    public static Color Surface => _current.Surface;
    public static Color SurfaceAlt => _current.SurfaceAlt;
    public static Color Border => _current.Border;
    public static Color TextPrimary => _current.TextPrimary;
    public static Color TextSecondary => _current.TextSecondary;
    public static Color Accent => _current.Accent;
    public static Color AccentStrong => _current.AccentStrong;
    public static Color AccentSoft => _current.AccentSoft;
    public static Color Success => _current.Success;
    public static Color Warning => _current.Warning;
    public static Color Danger => _current.Danger;

    public static void ApplyPreset(AppThemePreset preset)
    {
        _current = CreatePalette(preset);
    }

    public static Font TitleFont(float size = 18f)
        => new("Bahnschrift SemiBold", size, FontStyle.Regular, GraphicsUnit.Point);

    public static Font BodyFont(float size = 10f)
        => new("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);

    public static Font CaptionFont(float size = 9f)
        => new("Bahnschrift SemiBold", size, FontStyle.Regular, GraphicsUnit.Point);

    public static Button CreateButton(string text, bool primary = false, int width = 0)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Width = width > 0 ? width : (primary ? 152 : 132),
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : SurfaceAlt,
            ForeColor = primary ? Color.White : TextPrimary,
            Font = CaptionFont(9.5f),
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };

        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = primary ? AccentStrong : Border;
        btn.FlatAppearance.MouseDownBackColor = primary
            ? Darken(Accent, 0.16f)
            : Darken(SurfaceAlt, 0.10f);
        btn.FlatAppearance.MouseOverBackColor = primary
            ? AccentStrong
            : Brighten(SurfaceAlt, 0.06f);

        return btn;
    }

    public static TextBox StyleTextBox(TextBox textBox, bool multiline = false)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = BodyFont();
        textBox.BackColor = SurfaceAlt;
        textBox.ForeColor = TextPrimary;
        textBox.Multiline = multiline;
        return textBox;
    }

    public static ComboBox StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.Font = BodyFont();
        comboBox.BackColor = SurfaceAlt;
        comboBox.ForeColor = TextPrimary;
        return comboBox;
    }

    public static ListBox StyleListBox(ListBox listBox)
    {
        listBox.BorderStyle = BorderStyle.None;
        listBox.Font = BodyFont(10.5f);
        listBox.BackColor = Surface;
        listBox.ForeColor = TextPrimary;
        listBox.IntegralHeight = false;
        return listBox;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = CaptionFont(9.5f);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(22, AccentStrong.R, AccentStrong.G, AccentStrong.B);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = BodyFont();
        grid.GridColor = Border;
        grid.RowTemplate.Height = 34;
    }

    public static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = TitleFont(13f),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 6)
        };
    }

    public static Label CreateBodyLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = BodyFont(),
            ForeColor = TextSecondary
        };
    }

    public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var maxRadius = Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2);
        radius = Math.Clamp(radius, 1, maxRadius);
        var diameter = radius * 2;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static ThemePalette CreatePalette(AppThemePreset preset)
    {
        return preset switch
        {
            AppThemePreset.Midnight => new ThemePalette(
                Canvas: Color.FromArgb(18, 23, 34),
                Surface: Color.FromArgb(28, 36, 50),
                SurfaceAlt: Color.FromArgb(39, 49, 66),
                Border: Color.FromArgb(67, 88, 114),
                TextPrimary: Color.FromArgb(238, 244, 252),
                TextSecondary: Color.FromArgb(155, 173, 197),
                Accent: Color.FromArgb(37, 99, 235),
                AccentStrong: Color.FromArgb(59, 130, 246),
                AccentSoft: Color.FromArgb(191, 219, 254),
                Success: Color.FromArgb(34, 197, 94),
                Warning: Color.FromArgb(245, 158, 11),
                Danger: Color.FromArgb(239, 68, 68)),
            AppThemePreset.Ember => new ThemePalette(
                Canvas: Color.FromArgb(34, 28, 28),
                Surface: Color.FromArgb(50, 41, 41),
                SurfaceAlt: Color.FromArgb(64, 53, 53),
                Border: Color.FromArgb(95, 78, 74),
                TextPrimary: Color.FromArgb(248, 239, 235),
                TextSecondary: Color.FromArgb(193, 171, 165),
                Accent: Color.FromArgb(194, 65, 12),
                AccentStrong: Color.FromArgb(234, 88, 12),
                AccentSoft: Color.FromArgb(254, 215, 170),
                Success: Color.FromArgb(34, 197, 94),
                Warning: Color.FromArgb(251, 191, 36),
                Danger: Color.FromArgb(239, 68, 68)),
            AppThemePreset.Frost => new ThemePalette(
                Canvas: Color.FromArgb(27, 36, 44),
                Surface: Color.FromArgb(39, 50, 59),
                SurfaceAlt: Color.FromArgb(52, 66, 76),
                Border: Color.FromArgb(88, 109, 123),
                TextPrimary: Color.FromArgb(240, 248, 252),
                TextSecondary: Color.FromArgb(174, 191, 201),
                Accent: Color.FromArgb(8, 145, 178),
                AccentStrong: Color.FromArgb(6, 182, 212),
                AccentSoft: Color.FromArgb(165, 243, 252),
                Success: Color.FromArgb(22, 163, 74),
                Warning: Color.FromArgb(217, 119, 6),
                Danger: Color.FromArgb(220, 38, 38)),
            _ => new ThemePalette(
                Canvas: Color.FromArgb(30, 33, 38),
                Surface: Color.FromArgb(42, 46, 53),
                SurfaceAlt: Color.FromArgb(54, 59, 68),
                Border: Color.FromArgb(68, 75, 88),
                TextPrimary: Color.FromArgb(236, 240, 245),
                TextSecondary: Color.FromArgb(160, 170, 185),
                Accent: Color.FromArgb(15, 118, 110),
                AccentStrong: Color.FromArgb(13, 148, 136),
                AccentSoft: Color.FromArgb(204, 251, 241),
                Success: Color.FromArgb(22, 163, 74),
                Warning: Color.FromArgb(217, 119, 6),
                Danger: Color.FromArgb(220, 38, 38))
        };
    }

    private static Color Darken(Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            color.A,
            Math.Max(0, (int)(color.R * (1f - amount))),
            Math.Max(0, (int)(color.G * (1f - amount))),
            Math.Max(0, (int)(color.B * (1f - amount))));
    }

    private static Color Brighten(Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            color.A,
            Math.Min(255, color.R + (int)((255 - color.R) * amount)),
            Math.Min(255, color.G + (int)((255 - color.G) * amount)),
            Math.Min(255, color.B + (int)((255 - color.B) * amount)));
    }
}
