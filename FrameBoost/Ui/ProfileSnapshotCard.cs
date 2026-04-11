using System.Drawing.Drawing2D;
using FrameBoost.Models;

namespace FrameBoost.Ui;

/// <summary>
/// Animated profile snapshot card shown in the Profiles tab right panel.
/// Displays key settings for the selected profile with fade-in on change.
/// Uses the same CardPanel spotlight/hover aesthetic as the rest of the UI.
/// </summary>
internal sealed class ProfileSnapshotCard : CardPanel
{
    private GameProfile? _profile;
    private float _fadeAnim = 1f;
    private bool _fadingOut;
    private GameProfile? _pendingProfile;
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 16 };

    // Layout labels — updated on profile change
    private readonly Label _nameLabel;
    private readonly Label _exeLabel;
    private readonly Label _presetLabel;
    private readonly Label _autoLabel;
    private readonly Label _priorityLabel;
    private readonly Label _powerLabel;
    private readonly Label _bgLabel;
    private readonly Label _tweaksLabel;
    private readonly Label _emptyHint;

    public ProfileSnapshotCard()
    {
        FillColor   = AppTheme.Surface;
        BorderColor = AppTheme.Border;
        CornerRadius = 18;
        AutoSize     = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize  = new Size(320, 180);
        Padding      = new Padding(22);

        var layout = new TableLayoutPanel
        {
            Dock         = DockStyle.Top,
            ColumnCount  = 2,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor    = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label MakeKey(string text) => new()
        {
            Text      = text,
            AutoSize  = true,
            Font      = AppTheme.CaptionFont(9f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin    = new Padding(0, 0, 14, 6)
        };
        Label MakeVal() => new()
        {
            AutoSize  = true,
            Font      = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin    = new Padding(0, 0, 0, 6)
        };

        _nameLabel    = new Label { AutoSize = true, Font = AppTheme.TitleFont(14f), ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
        _exeLabel     = MakeVal();
        _presetLabel  = MakeVal();
        _autoLabel    = MakeVal();
        _priorityLabel = MakeVal();
        _powerLabel   = MakeVal();
        _bgLabel      = MakeVal();
        _tweaksLabel  = MakeVal();
        _tweaksLabel.MaximumSize = new Size(360, 0);

        _emptyHint = new Label
        {
            Text      = "Select a profile from the list to see its settings here.",
            AutoSize  = true,
            MaximumSize = new Size(360, 0),
            Font      = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin    = new Padding(0, 8, 0, 0)
        };

        // Row 0 — name spans both columns
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_nameLabel, 0, 0);
        layout.SetColumnSpan(_nameLabel, 2);

        void AddRow(string key, Label val, int row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(MakeKey(key), 0, row);
            layout.Controls.Add(val, 1, row);
        }

        AddRow("Executable",   _exeLabel,      1);
        AddRow("Preset",       _presetLabel,   2);
        AddRow("Auto-boost",   _autoLabel,     3);
        AddRow("Game priority",_priorityLabel, 4);
        AddRow("Power plan",   _powerLabel,    5);
        AddRow("Background",   _bgLabel,       6);
        AddRow("Tweaks",       _tweaksLabel,   7);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_emptyHint, 0, 8);
        layout.SetColumnSpan(_emptyHint, 2);

        Controls.Add(layout);

        _fadeTimer.Tick += (_, _) =>
        {
            if (_fadingOut)
            {
                _fadeAnim -= 0.12f;
                if (_fadeAnim <= 0f)
                {
                    _fadeAnim = 0f;
                    _fadingOut = false;
                    _profile = _pendingProfile;
                    RefreshLabels();
                }
            }
            else
            {
                _fadeAnim += 0.10f;
                if (_fadeAnim >= 1f) { _fadeAnim = 1f; _fadeTimer.Stop(); }
            }
            Invalidate(true);
        };

        RefreshLabels();
    }

    public void SetProfile(GameProfile? profile)
    {
        if (ReferenceEquals(_profile, profile)) return;
        _pendingProfile = profile;
        _fadingOut = true;
        _fadeAnim = _fadeAnim > 0.05f ? _fadeAnim : 0.05f;
        _fadeTimer.Start();
    }

    private void RefreshLabels()
    {
        var p = _profile;
        bool hasProfile = p is not null && p.Id != "__none__";

        _emptyHint.Visible = !hasProfile;
        _nameLabel.Visible  = hasProfile;

        if (!hasProfile)
        {
            _nameLabel.Text     = string.Empty;
            _exeLabel.Text      = string.Empty;
            _presetLabel.Text   = string.Empty;
            _autoLabel.Text     = string.Empty;
            _priorityLabel.Text = string.Empty;
            _powerLabel.Text    = string.Empty;
            _bgLabel.Text       = string.Empty;
            _tweaksLabel.Text   = string.Empty;
            return;
        }

        _nameLabel.Text     = p!.Name;
        _exeLabel.Text      = string.IsNullOrWhiteSpace(p.ExecutablePath)
            ? "—" : Path.GetFileName(p.ExecutablePath);
        _presetLabel.Text   = p.BoostPreset.ToString();
        _presetLabel.ForeColor = p.BoostPreset switch
        {
            BoostPresetOption.MaxFps      => AppTheme.Warning,
            BoostPresetOption.Performance => AppTheme.AccentStrong,
            _                            => AppTheme.TextPrimary
        };
        _autoLabel.Text = p.AutoBoost ? "✓ Enabled" : "✗ Disabled";
        _autoLabel.ForeColor = p.AutoBoost ? AppTheme.Success : AppTheme.TextSecondary;
        _priorityLabel.Text = p.BoostGamePriority ? p.GamePriority.ToString() : "Unchanged";
        _priorityLabel.ForeColor = p.BoostGamePriority ? AppTheme.AccentSoft : AppTheme.TextSecondary;
        _powerLabel.Text = p.SwitchPowerPlan
            ? (string.IsNullOrWhiteSpace(p.PreferredPowerPlanName) ? "Auto-select" : p.PreferredPowerPlanName)
            : "Unchanged";
        _powerLabel.ForeColor = p.SwitchPowerPlan ? AppTheme.TextPrimary : AppTheme.TextSecondary;

        var bgTargets = p.GetBackgroundProcessNames().ToList();
        _bgLabel.Text = bgTargets.Count == 0
            ? "None"
            : $"{bgTargets.Count} targets · " +
              (p.LowerBackgroundProcesses ? "priority" : "") +
              (p.TrimBackgroundMemory ? " + trim" : "") +
              (p.CloseBackgroundAppsGracefully ? " + close" : "");
        _bgLabel.ForeColor = bgTargets.Count > 0 ? AppTheme.TextPrimary : AppTheme.TextSecondary;

        var tweaks = new List<string>();
        if (p.ShouldApplyBackgroundEcoQos())  tweaks.Add("EcoQoS");
        if (p.BoostGamePriority)              tweaks.Add($"{p.GamePriority} priority");
        _tweaksLabel.Text = tweaks.Count > 0 ? string.Join(" · ", tweaks) : "Default";
        _tweaksLabel.ForeColor = tweaks.Count > 0 ? AppTheme.AccentSoft : AppTheme.TextSecondary;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_fadeAnim >= 1f) return;

        // Fade overlay drawn on top of everything
        int alpha = (int)((1f - _fadeAnim) * 210);
        using var fadeBrush = new SolidBrush(Color.FromArgb(alpha, AppTheme.Surface));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var fadePath = AppTheme.CreateRoundedRectangle(rect, CornerRadius);
        e.Graphics.FillPath(fadeBrush, fadePath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fadeTimer.Dispose();
        base.Dispose(disposing);
    }
}
