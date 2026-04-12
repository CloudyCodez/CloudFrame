using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class ProfileSnapshotCard : CardPanel
{
    private readonly BannerPanel _banner;
    private readonly AvatarBadge _avatar;
    private readonly Label _titleLabel;
    private readonly TextBox _nameEditor;
    private readonly Label _pathLabel;
    private readonly FlowLayoutPanel _pillFlow;
    private readonly Label _activityTitle;
    private readonly Label _activityDetail;
    private readonly SectionCard _infoSection;
    private readonly SectionCard _prefsSection;
    private readonly SectionCard _statsSection;
    private readonly SectionCard _syncSection;
    private readonly Label _emptyHint;
    private readonly Label _exeValue;
    private readonly Label _presetValue;
    private readonly Label _powerValue;
    private readonly Label _priorityValue;
    private readonly Label _targetsValue;
    private readonly Label _cadenceValue;
    private readonly Label _postureValue;
    private readonly Label _memoryValue;
    private readonly Label _updatesValue;
    private readonly Label _supportValue;
    private readonly CheckBox _autoToggle;
    private readonly CheckBox _powerToggle;
    private readonly CheckBox _backgroundToggle;
    private readonly ComboBox _presetCombo;
    private readonly List<StatusPill> _pills = [];
    private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 90 };
    private readonly FlowLayoutPanel _toolbar;
    private readonly Control _headerTitleStack;

    private GameProfile? _profile;
    private SessionReport? _sessionReport;
    private GameRecommendationMemory? _recommendationMemory;
    private TelemetrySnapshot _telemetry = new();
    private bool _hasActiveSession;
    private bool _updateChecksEnabled;
    private bool _syncing;
    private int _pulseTick;

    public event EventHandler? EditRequested;
    public event EventHandler? SyncRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? FeedbackRequested;
    public event EventHandler? ProfileChanged;

    public ProfileSnapshotCard()
    {
        FillColor = AppTheme.Canvas;
        BorderColor = AppTheme.Border;
        CornerRadius = 24;
        EnableSpotlight = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(0);

        var root = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, BackColor = Color.Transparent };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _banner = new BannerPanel { Height = 172, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(24) };
        root.Controls.Add(_banner, 0, 0);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _avatar = new AvatarBadge { Image = BrandAssets.GetDashboardWatermark(), Margin = new Padding(0, 0, 18, 0) };
        header.Controls.Add(_avatar, 0, 0);
        header.SetRowSpan(_avatar, 3);

        var titleStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Dock = DockStyle.Fill };
        _headerTitleStack = titleStack;
        titleStack.Controls.Add(new Label { Text = "PROFILE STUDIO", AutoSize = true, Font = AppTheme.CaptionFont(9.5f), ForeColor = Color.FromArgb(210, AppTheme.AccentSoft), BackColor = Color.Transparent });
        _titleLabel = new Label { AutoSize = true, Font = AppTheme.TitleFont(22f), ForeColor = Color.White, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 0) };
        _nameEditor = AppTheme.StyleTextBox(new TextBox { Visible = false, Width = 260 });
        _nameEditor.KeyDown += HandleNameEditorKeyDown;
        _nameEditor.Leave += (_, _) => { if (_nameEditor.Visible) CommitNameEdit(); };
        _pathLabel = new Label { AutoSize = true, MaximumSize = new Size(520, 0), Font = AppTheme.BodyFont(10f), ForeColor = Color.FromArgb(214, 230, 246), BackColor = Color.Transparent, Margin = new Padding(0, 6, 0, 0) };
        _pillFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Color.Transparent, Margin = new Padding(0, 12, 0, 0) };
        titleStack.Controls.Add(_titleLabel);
        titleStack.Controls.Add(_nameEditor);
        titleStack.Controls.Add(_pathLabel);
        titleStack.Controls.Add(_pillFlow);
        header.Controls.Add(titleStack, 1, 0);

        _toolbar = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Color.Transparent, Dock = DockStyle.Right };
        _toolbar.Controls.Add(CreateToolbarButton("Edit", (_, _) => BeginNameEdit()));
        _toolbar.Controls.Add(CreateToolbarButton("Sync", (_, _) => SyncRequested?.Invoke(this, EventArgs.Empty)));
        _toolbar.Controls.Add(CreateToolbarButton("Settings", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        _toolbar.Controls.Add(CreateToolbarButton("Feedback", (_, _) => FeedbackRequested?.Invoke(this, EventArgs.Empty)));
        header.Controls.Add(_toolbar, 2, 0);

        var activity = new CardPanel { FillColor = Color.FromArgb(42, 59, 86), BorderColor = Color.FromArgb(84, 130, 204), CornerRadius = 16, Dock = DockStyle.Fill, Padding = new Padding(16), Margin = new Padding(0, 14, 0, 0), Height = 78 };
        _activityTitle = new Label { AutoSize = true, Font = AppTheme.CaptionFont(10f), ForeColor = AppTheme.AccentSoft, BackColor = Color.Transparent };
        _activityDetail = new Label { AutoSize = true, MaximumSize = new Size(560, 0), Font = AppTheme.BodyFont(9.8f), ForeColor = Color.White, BackColor = Color.Transparent, Margin = new Padding(0, 8, 0, 0) };
        activity.Controls.Add(_activityTitle);
        activity.Controls.Add(_activityDetail);
        _activityDetail.Location = new Point(0, 22);
        header.Controls.Add(activity, 1, 1);
        header.SetColumnSpan(activity, 2);
        _banner.Controls.Add(header);

        var modules = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, BackColor = Color.Transparent, Padding = new Padding(18, 0, 18, 18) };
        modules.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _infoSection = new SectionCard("Profile Info", "Core launch identity, path, and preset.") { Dock = DockStyle.Top };
        _prefsSection = new SectionCard("Preferences", "Inline controls for the main boost behavior.") { Dock = DockStyle.Top };
        _statsSection = new SectionCard("System Stats", "Live session posture and optimization cadence.") { Dock = DockStyle.Top };
        _syncSection = new SectionCard("Cloud Sync", "Recommendation memory, updates, and support workflow.") { Dock = DockStyle.Top };

        _exeValue = CreateValueLabel(); _presetValue = CreateValueLabel(); _powerValue = CreateValueLabel(); _priorityValue = CreateValueLabel();
        _infoSection.AddDetailRow("Executable", _exeValue);
        _infoSection.AddDetailRow("Preset", _presetValue);
        _infoSection.AddDetailRow("Power plan", _powerValue);
        _infoSection.AddDetailRow("Priority", _priorityValue);

        _presetCombo = AppTheme.StyleComboBox(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 });
        _presetCombo.Items.AddRange(Enum.GetNames<BoostPresetOption>());
        _presetCombo.SelectedIndexChanged += (_, _) => { if (!_syncing && _profile is not null && _presetCombo.SelectedIndex >= 0) { var profile = _profile!; profile.BoostPreset = Enum.Parse<BoostPresetOption>(_presetCombo.SelectedItem!.ToString()!); OnProfileChanged(); } };
        _autoToggle = CreateToggle("Auto boost this title");
        _autoToggle.CheckedChanged += (_, _) => { if (!_syncing && _profile is not null) { var profile = _profile!; profile.AutoBoost = _autoToggle.Checked; OnProfileChanged(); } };
        _powerToggle = CreateToggle("Switch power plan while gaming");
        _powerToggle.CheckedChanged += (_, _) => { if (!_syncing && _profile is not null) { var profile = _profile!; profile.SwitchPowerPlan = _powerToggle.Checked; OnProfileChanged(); } };
        _backgroundToggle = CreateToggle("Manage listed background apps");
        _backgroundToggle.CheckedChanged += (_, _) => { if (!_syncing && _profile is not null) { var profile = _profile!; profile.LowerBackgroundProcesses = _backgroundToggle.Checked; OnProfileChanged(); } };
        _prefsSection.AddInlineEditor("Preset", _presetCombo);
        _prefsSection.AddControl(_autoToggle);
        _prefsSection.AddControl(_powerToggle);
        _prefsSection.AddControl(_backgroundToggle);

        _targetsValue = CreateValueLabel(); _cadenceValue = CreateValueLabel(); _postureValue = CreateValueLabel();
        _statsSection.AddDetailRow("Background targets", _targetsValue);
        _statsSection.AddDetailRow("Maintenance cadence", _cadenceValue);
        _statsSection.AddDetailRow("Live posture", _postureValue);

        _memoryValue = CreateValueLabel(); _updatesValue = CreateValueLabel(); _supportValue = CreateValueLabel();
        _syncSection.AddDetailRow("CloudFrame memory", _memoryValue);
        _syncSection.AddDetailRow("Update channel", _updatesValue);
        _syncSection.AddDetailRow("Support snapshot", _supportValue);

        modules.Controls.Add(_infoSection, 0, 0);
        modules.Controls.Add(_prefsSection, 0, 1);
        modules.Controls.Add(_statsSection, 0, 2);
        modules.Controls.Add(_syncSection, 0, 3);
        root.Controls.Add(modules, 0, 1);

        _emptyHint = new Label { Text = "Select a profile to open its workspace.", AutoSize = true, MaximumSize = new Size(560, 0), Font = AppTheme.BodyFont(10f), ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent, Margin = new Padding(24, 0, 24, 22) };
        root.Controls.Add(_emptyHint, 0, 2);
        Controls.Add(root);

        _pulseTimer.Tick += (_, _) => { _pulseTick++; var pulse = 0.55f + ((MathF.Sin(_pulseTick / 4.5f) + 1f) * 0.225f); foreach (var pill in _pills) pill.Pulse = pulse; };
        _pulseTimer.Start();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        RefreshDisplay();
    }

    public void SetProfile(GameProfile? profile) { _profile = profile; RefreshDisplay(); }
    public void SetAnimationEnabled(bool enabled)
    {
        _banner.SetAnimationEnabled(enabled);
        if (enabled && Visible)
        {
            _pulseTimer.Start();
        }
        else
        {
            _pulseTimer.Stop();
        }
    }
    public void SetContext(SessionReport? report, GameRecommendationMemory? memory, TelemetrySnapshot telemetry, bool hasActiveSession, bool updateChecksEnabled)
    {
        _sessionReport = report; _recommendationMemory = memory; _telemetry = telemetry; _hasActiveSession = hasActiveSession; _updateChecksEnabled = updateChecksEnabled; RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        SuspendLayout();
        _pillFlow.SuspendLayout();
        var profile = _profile;
        var hasProfile = profile is not null && profile.Id != "__none__";
        _banner.Visible = hasProfile; _infoSection.Visible = hasProfile; _prefsSection.Visible = hasProfile; _statsSection.Visible = hasProfile; _syncSection.Visible = hasProfile; _emptyHint.Visible = !hasProfile;
        if (!hasProfile)
        {
            _pillFlow.Controls.Clear();
            _pillFlow.ResumeLayout(true);
            ResumeLayout(true);
            return;
        }

        _syncing = true;
        try
        {
            _titleLabel.Text = profile!.Name;
            _nameEditor.Text = profile.Name;
            _pathLabel.Text = string.IsNullOrWhiteSpace(profile.ExecutablePath) ? "Bind the real executable to unlock stronger launcher handoff and smarter game matching." : profile.ExecutablePath;
            _exeValue.Text = string.IsNullOrWhiteSpace(profile.ExecutablePath) ? "Not set" : profile.ExecutablePath;
            _presetValue.Text = profile.BoostPreset.ToString();
            _powerValue.Text = profile.SwitchPowerPlan ? (string.IsNullOrWhiteSpace(profile.PreferredPowerPlanName) ? "Auto-select active gaming plan" : profile.PreferredPowerPlanName) : "Leave power plan unchanged";
            _priorityValue.Text = profile.BoostGamePriority ? profile.GamePriority.ToString() : "No game priority lift";
            _presetCombo.SelectedItem = profile.BoostPreset.ToString();
            _autoToggle.Checked = profile.AutoBoost; _powerToggle.Checked = profile.SwitchPowerPlan; _backgroundToggle.Checked = profile.LowerBackgroundProcesses;

            var targets = profile.GetBackgroundProcessNames().ToList();
            _targetsValue.Text = targets.Count == 0 ? "No listed background targets" : $"{targets.Count} safe targets in watch list";
            _cadenceValue.Text = profile.ShouldRunRecurringMaintenance() ? $"Refresh every {Math.Round(profile.GetMaintenanceInterval().TotalSeconds)}s" : "One-shot boost only";
            _postureValue.Text = _hasActiveSession ? $"Live session | CPU {Math.Round(_telemetry.CpuPercent)}% | GPU {Math.Round(_telemetry.GpuPercent ?? 0)}%" : "Idle profile workspace";
            _memoryValue.Text = _recommendationMemory is null ? "No remembered recommendation yet" : $"{_recommendationMemory.RecommendedTitle} | saved {FormatAge(_recommendationMemory.UpdatedAt)}";
            _updatesValue.Text = _updateChecksEnabled ? "GitHub release checks enabled" : "Manual update checks only";
            _supportValue.Text = _sessionReport is null || !string.Equals(_sessionReport.ProfileName, profile.Name, StringComparison.OrdinalIgnoreCase) ? "Export Issue Report anytime from Tools" : _sessionReport.Summary;

            var utilityCount = new[] { profile.SwitchPowerPlan, profile.BoostGamePriority, profile.LowerBackgroundProcesses, profile.TrimBackgroundMemory, profile.UseBackgroundEcoQos }.Count(static x => x);
            _activityTitle.Text = _hasActiveSession ? "LIVE ACTIVITY SNAPSHOT" : "READY SNAPSHOT";
            _activityDetail.Text = _sessionReport is not null && string.Equals(_sessionReport.ProfileName, profile.Name, StringComparison.OrdinalIgnoreCase)
                ? $"{_sessionReport.ResultLabel} | {_sessionReport.Detail}"
                : $"Last optimized: {(profile.AutoBoost ? "auto-armed" : "manual mode")} | {utilityCount} utilities active | {targets.Count} watched targets";

            var accent = profile.BoostPreset switch { BoostPresetOption.MaxFps => AppTheme.Warning, BoostPresetOption.Performance => AppTheme.AccentStrong, _ => Color.FromArgb(76, 114, 255) };
            _avatar.AccentColor = accent; _banner.AccentColor = accent; PopulatePills(profile, accent);
            UpdateResponsiveLayout();
        }
        finally
        {
            _syncing = false;
            _pillFlow.ResumeLayout(true);
            ResumeLayout(true);
        }
    }

    protected override void Dispose(bool disposing) { if (disposing) _pulseTimer.Dispose(); base.Dispose(disposing); }

    private void PopulatePills(GameProfile profile, Color accent)
    {
        _pillFlow.Controls.Clear(); _pills.Clear();
        AddPill(profile.BoostPreset.ToString(), accent, true);
        AddPill(profile.AutoBoost ? "Auto-armed" : "Manual", profile.AutoBoost ? AppTheme.Success : AppTheme.Warning, true);
        AddPill(_hasActiveSession ? "Live" : "Idle", _hasActiveSession ? AppTheme.AccentStrong : AppTheme.TextSecondary, _hasActiveSession);
        AddPill(_updateChecksEnabled ? "Connected" : "Offline", _updateChecksEnabled ? Color.FromArgb(88, 188, 255) : AppTheme.TextSecondary, _updateChecksEnabled);
    }

    private void AddPill(string text, Color accent, bool pulse)
    {
        var pill = new StatusPill { Text = text, AccentColor = accent, PulseEnabled = pulse };
        _pills.Add(pill);
        _pillFlow.Controls.Add(pill);
    }

    private void BeginNameEdit()
    {
        if (_profile is null) { EditRequested?.Invoke(this, EventArgs.Empty); return; }
        _titleLabel.Visible = false; _nameEditor.Visible = true; _nameEditor.Focus(); _nameEditor.SelectAll();
    }

    private void CommitNameEdit()
    {
        if (_profile is null) return;
        var newName = _nameEditor.Text.Trim();
        _nameEditor.Visible = false; _titleLabel.Visible = true;
        if (!string.IsNullOrWhiteSpace(newName) && !string.Equals(newName, _profile.Name, StringComparison.Ordinal)) { _profile.Name = newName; OnProfileChanged(); }
        else { _nameEditor.Text = _profile.Name; RefreshDisplay(); }
    }

    private void HandleNameEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; CommitNameEdit(); }
        else if (e.KeyCode == Keys.Escape) { e.Handled = true; e.SuppressKeyPress = true; _nameEditor.Text = _profile?.Name ?? string.Empty; _nameEditor.Visible = false; _titleLabel.Visible = true; }
    }

    private void OnProfileChanged() { RefreshDisplay(); ProfileChanged?.Invoke(this, EventArgs.Empty); }

    private void UpdateResponsiveLayout()
    {
        var contentWidth = Math.Max(320, Width - Padding.Horizontal - 80);
        _pathLabel.MaximumSize = new Size(Math.Max(280, contentWidth - 150), 0);
        _activityDetail.MaximumSize = new Size(Math.Max(280, contentWidth - 120), 0);
        _toolbar.FlowDirection = Width < 560 ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        _toolbar.WrapContents = Width >= 560;

        if (_headerTitleStack is FlowLayoutPanel stack)
        {
            stack.Width = Math.Max(280, contentWidth - (_toolbar.Width + 110));
        }
    }

    private static Button CreateToolbarButton(string text, EventHandler clickHandler)
    {
        var button = AppTheme.CreateButton(text, width: 96);
        button.Click += clickHandler;
        return button;
    }

    private static Label CreateValueLabel() => new() { AutoSize = true, MaximumSize = new Size(520, 0), Font = AppTheme.BodyFont(9.8f), ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent };

    private static CheckBox CreateToggle(string text)
    {
        var checkBox = new CheckBox { Text = text, AutoSize = true, Font = AppTheme.BodyFont(9.8f), ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
        return checkBox;
    }

    private static string FormatAge(DateTimeOffset timestamp)
    {
        var age = DateTimeOffset.Now - timestamp;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours < 1) return $"{Math.Max(1, (int)age.TotalMinutes)}m ago";
        if (age.TotalDays < 1) return $"{Math.Max(1, (int)age.TotalHours)}h ago";
        return $"{Math.Max(1, (int)age.TotalDays)}d ago";
    }
}
