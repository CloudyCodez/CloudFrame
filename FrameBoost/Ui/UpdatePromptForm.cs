using FrameBoost.Services;

namespace FrameBoost.Ui;

internal enum UpdatePromptChoice
{
    Later,
    OpenRelease,
    SkipVersion
}

internal sealed class UpdatePromptForm : Form
{
    public UpdatePromptChoice Choice { get; private set; } = UpdatePromptChoice.Later;

    public UpdatePromptForm(UpdateCheckResult result)
    {
        Text = "CloudFrame Update Available";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 440);
        Size = new Size(720, 520);
        BackColor = AppTheme.Canvas;
        Font = AppTheme.BodyFont();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = $"CloudFrame {result.LatestVersion} is ready",
            AutoSize = true,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.TitleFont(18f)
        }, 0, 0);

        var subtitle = $"{result.CurrentVersion} installed now";
        if (!string.IsNullOrWhiteSpace(result.ReleaseName))
        {
            subtitle += $"  •  {result.ReleaseName}";
        }

        if (result.PublishedAt is not null)
        {
            subtitle += $"  •  Released {result.PublishedAt.Value.LocalDateTime:g}";
        }

        root.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.BodyFont(10f),
            Margin = new Padding(0, 8, 0, 14)
        }, 0, 1);

        var notesCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            InnerPadding = new Padding(18)
        };

        var notesBox = AppTheme.StyleTextBox(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "No release notes were published for this release yet."
                : result.ReleaseNotes.Trim()
        });
        notesCard.Controls.Add(notesBox);
        root.Controls.Add(notesCard, 0, 2);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0, 14, 0, 0)
        };

        var updateButton = AppTheme.CreateButton("Open Release", primary: true, width: 152);
        updateButton.Click += (_, _) =>
        {
            Choice = UpdatePromptChoice.OpenRelease;
            DialogResult = DialogResult.OK;
        };

        var laterButton = AppTheme.CreateButton("Later", width: 100);
        laterButton.Click += (_, _) =>
        {
            Choice = UpdatePromptChoice.Later;
            DialogResult = DialogResult.Cancel;
        };

        var skipButton = AppTheme.CreateButton("Skip This Version", width: 152);
        skipButton.Click += (_, _) =>
        {
            Choice = UpdatePromptChoice.SkipVersion;
            DialogResult = DialogResult.OK;
        };

        buttonFlow.Controls.Add(updateButton);
        buttonFlow.Controls.Add(skipButton);
        buttonFlow.Controls.Add(laterButton);
        root.Controls.Add(buttonFlow, 0, 3);

        AcceptButton = updateButton;
        CancelButton = laterButton;
        Controls.Add(root);
    }
}
