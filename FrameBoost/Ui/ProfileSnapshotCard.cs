namespace FrameBoost.Ui;

using FrameBoost.Models;

internal sealed class ProfileSnapshotCard : CardPanel
{
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 16 };

    private readonly Label _nameLabel;
    private readonly Label _exeLabel;
    private readonly Label _presetLabel;
    private readonly Label _autoLabel;
    private readonly Label _priorityLabel;
    private readonly Label _powerLabel;
    private readonly Label _bgLabel;
    private readonly Label _tweaksLabel;
    private readonly Label _emptyHint;

    private GameProfile? _profile;
    private GameProfile? _pendingProfile;
    private float _fadeAmount = 1f;
    private bool _fadingOut;

    public ProfileSnapshotCard()
    {
        FillColor = AppTheme.Surface;
        BorderColor = AppTheme.Border;
        CornerRadius = 18;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(320, 180);
        Padding = new Padding(22);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label CreateKeyLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = AppTheme.CaptionFont(9f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 14, 6)
        };

        Label CreateValueLabel() => new()
        {
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };

        _nameLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10)
        };
        _exeLabel = CreateValueLabel();
        _presetLabel = CreateValueLabel();
        _autoLabel = CreateValueLabel();
        _priorityLabel = CreateValueLabel();
        _powerLabel = CreateValueLabel();
        _bgLabel = CreateValueLabel();
        _tweaksLabel = CreateValueLabel();
        _tweaksLabel.MaximumSize = new Size(360, 0);

        _emptyHint = new Label
        {
            Text = "Select a profile from the list to see its settings here.",
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_nameLabel, 0, 0);
        layout.SetColumnSpan(_nameLabel, 2);

        void AddRow(string key, Label value, int row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(CreateKeyLabel(key), 0, row);
            layout.Controls.Add(value, 1, row);
        }

        AddRow("Executable", _exeLabel, 1);
        AddRow("Preset", _presetLabel, 2);
        AddRow("Auto-boost", _autoLabel, 3);
        AddRow("Game priority", _priorityLabel, 4);
        AddRow("Power plan", _powerLabel, 5);
        AddRow("Background", _bgLabel, 6);
        AddRow("Tweaks", _tweaksLabel, 7);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_emptyHint, 0, 8);
        layout.SetColumnSpan(_emptyHint, 2);

        Controls.Add(layout);

        _fadeTimer.Tick += (_, _) =>
        {
            if (_fadingOut)
            {
                _fadeAmount -= 0.12f;
                if (_fadeAmount <= 0f)
                {
                    _fadeAmount = 0f;
                    _fadingOut = false;
                    _profile = _pendingProfile;
                    RefreshLabels();
                }
            }
            else
            {
                _fadeAmount += 0.10f;
                if (_fadeAmount >= 1f)
                {
                    _fadeAmount = 1f;
                    _fadeTimer.Stop();
                }
            }

            Invalidate(true);
        };

        RefreshLabels();
    }

    public void SetProfile(GameProfile? profile)
    {
        if (ReferenceEquals(_profile, profile))
        {
            return;
        }

        _pendingProfile = profile;
        _fadingOut = true;
        _fadeAmount = _fadeAmount > 0.05f ? _fadeAmount : 0.05f;
        _fadeTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_fadeAmount >= 1f)
        {
            return;
        }

        var alpha = (int)((1f - _fadeAmount) * 210);
        using var fadeBrush = new SolidBrush(Color.FromArgb(alpha, AppTheme.Surface));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var fadePath = AppTheme.CreateRoundedRectangle(rect, CornerRadius);
        e.Graphics.FillPath(fadeBrush, fadePath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RefreshLabels()
    {
        var profile = _profile;
        var hasProfile = profile is not null && profile.Id != "__none__";

        _emptyHint.Visible = !hasProfile;
        _nameLabel.Visible = hasProfile;

        if (!hasProfile)
        {
            _nameLabel.Text = string.Empty;
            _exeLabel.Text = string.Empty;
            _presetLabel.Text = string.Empty;
            _autoLabel.Text = string.Empty;
            _priorityLabel.Text = string.Empty;
            _powerLabel.Text = string.Empty;
            _bgLabel.Text = string.Empty;
            _tweaksLabel.Text = string.Empty;
            return;
        }

        _nameLabel.Text = profile!.Name;
        _exeLabel.Text = string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? "Not set"
            : Path.GetFileName(profile.ExecutablePath);
        _presetLabel.Text = profile.BoostPreset.ToString();
        _presetLabel.ForeColor = profile.BoostPreset switch
        {
            BoostPresetOption.MaxFps => AppTheme.Warning,
            BoostPresetOption.Performance => AppTheme.AccentStrong,
            _ => AppTheme.TextPrimary
        };

        _autoLabel.Text = profile.AutoBoost ? "Enabled" : "Disabled";
        _autoLabel.ForeColor = profile.AutoBoost ? AppTheme.Success : AppTheme.TextSecondary;

        _priorityLabel.Text = profile.BoostGamePriority ? profile.GamePriority.ToString() : "Unchanged";
        _priorityLabel.ForeColor = profile.BoostGamePriority ? AppTheme.AccentSoft : AppTheme.TextSecondary;

        _powerLabel.Text = profile.SwitchPowerPlan
            ? (string.IsNullOrWhiteSpace(profile.PreferredPowerPlanName) ? "Auto-select" : profile.PreferredPowerPlanName)
            : "Unchanged";
        _powerLabel.ForeColor = profile.SwitchPowerPlan ? AppTheme.TextPrimary : AppTheme.TextSecondary;

        var backgroundTargets = profile.GetBackgroundProcessNames().ToList();
        var backgroundActions = new List<string>();
        if (profile.LowerBackgroundProcesses) backgroundActions.Add("priority");
        if (profile.TrimBackgroundMemory) backgroundActions.Add("trim");
        if (profile.CloseBackgroundAppsGracefully) backgroundActions.Add("close");

        _bgLabel.Text = backgroundTargets.Count == 0
            ? "None"
            : backgroundActions.Count == 0
                ? $"{backgroundTargets.Count} targets"
                : $"{backgroundTargets.Count} targets | {string.Join(", ", backgroundActions)}";
        _bgLabel.ForeColor = backgroundTargets.Count > 0 ? AppTheme.TextPrimary : AppTheme.TextSecondary;

        var tweaks = new List<string>();
        if (profile.ShouldApplyBackgroundEcoQos()) tweaks.Add("EcoQoS");
        if (profile.BoostGamePriority) tweaks.Add($"{profile.GamePriority} priority");

        _tweaksLabel.Text = tweaks.Count > 0 ? string.Join(" | ", tweaks) : "Default";
        _tweaksLabel.ForeColor = tweaks.Count > 0 ? AppTheme.AccentSoft : AppTheme.TextSecondary;
    }
}
