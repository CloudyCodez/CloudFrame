using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal static class AppTheme
{
    public static Color Canvas => Color.FromArgb(30, 33, 38);

    public static Color Surface => Color.FromArgb(42, 46, 53);

    public static Color SurfaceAlt => Color.FromArgb(54, 59, 68);

    public static Color Border => Color.FromArgb(68, 75, 88);

    public static Color TextPrimary => Color.FromArgb(236, 240, 245);

    public static Color TextSecondary => Color.FromArgb(160, 170, 185);

    public static Color Accent => Color.FromArgb(15, 118, 110);

    public static Color AccentStrong => Color.FromArgb(13, 148, 136);

    public static Color AccentSoft => Color.FromArgb(204, 251, 241);

    public static Color Success => Color.FromArgb(22, 163, 74);

    public static Color Warning => Color.FromArgb(217, 119, 6);

    public static Color Danger => Color.FromArgb(220, 38, 38);

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
            ? Color.FromArgb(10, 100, 92)
            : Color.FromArgb(64, 70, 82);
        btn.FlatAppearance.MouseOverBackColor = primary
            ? AccentStrong
            : Color.FromArgb(62, 68, 80);

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
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(22, 80, 75);
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
        // Clamp radius so diameter never exceeds the smaller dimension
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
}
