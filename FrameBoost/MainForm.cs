using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using FrameBoost.Core;
using FrameBoost.Models;
using FrameBoost.Services;
using FrameBoost.Ui;

namespace FrameBoost;

internal sealed class MainForm : Form
{
    private readonly Logger _logger = new();
    private readonly SettingsService _settingsService = new();
    private readonly RecoveryStateService _recoveryStateService = new();
    private readonly AntiCheatCompatibilityService _antiCheatCompatibilityService = new();
    private readonly SystemTelemetryService _systemTelemetryService = new();
    private readonly PresentMonFpsService _presentMonFpsService;
    private readonly FpsComparisonTracker _fpsComparisonTracker = new();
    private readonly ProcessService _processService = new();
    private readonly PriorityService _priorityService = new();
    private readonly ShaderCacheService _shaderCacheService = new();
    private readonly TimerResolutionService _timerResolutionService;
    private readonly WindowsBoostRegistryService _registryBoostService;
    private readonly System.Windows.Forms.Timer _telemetryTimer = new() { Interval = 1500 };
    private readonly SemaphoreSlim _telemetryRefreshGate = new(1, 1);

    private readonly AppSettings _settings;
    private readonly PowerPlanService _powerPlanService;
    private readonly BoostCoordinator _boostCoordinator;
    private readonly GameDetectionService _detectionService;

    private readonly List<DetectedGame> _detectedGames = [];
    private readonly List<PowerPlanInfo> _powerPlans = [];

    // Preset card instances — kept as fields so we can update IsSelected on profile change
    private BoostPresetCard _balancedCard   = null!;
    private BoostPresetCard _performanceCard = null!;
    private BoostPresetCard _maxFpsCard      = null!;
    private ListBox _profilesList = null!;
    private DataGridView _detectedGrid = null!;
    private TextBox _logTextBox = null!;
    private TextBox _detectedFilterTextBox = null!;
    // Live stat rings on the dashboard
    private LiveStatRing _cpuRing  = null!;
    private LiveStatRing _gpuRing  = null!;
    private LiveStatRing _fpsRing  = null!;
    // Profile snapshot card
    private ProfileSnapshotCard _profileSnapshotCard = null!;
    // Boost control toggle chips
    private ToggleChip _switchPowerPlanChip        = null!;
    private ToggleChip _boostGamePriorityChip      = null!;
    private ToggleChip _lowerBackgroundChip        = null!;
    private ToggleChip _trimBackgroundMemoryChip   = null!;
    private ToggleChip _closeBackgroundAppsChip    = null!;
    private ToggleChip _memoryPriorityChip         = null!;
    private ToggleChip _ecoQosChip                 = null!;
    private ToggleChip _maintenanceChip            = null!;
    private CheckBox _overlayToggleCheckBox = null!;
    private ComboBox _overlayPositionComboBox = null!;
    private ComboBox _overlayStyleComboBox = null!;
    private CheckBox _overlayShowFpsCheckBox = null!;
    private CheckBox _overlayShowCpuCheckBox = null!;
    private CheckBox _overlayShowGpuCheckBox = null!;
    private Panel   _overlayAccentPreview   = null!;
    private Label _monitoringLabel = null!;
    private Label _sessionLabel = null!;
    private Label _elevationLabel = null!;
    private Label _selectedProfileLabel = null!;
    private Label _presetSummaryLabel = null!;
    private Label _boostControlScopeLabel = null!;
    private Label _boostControlHintLabel = null!;
    private Label _safeTargetsLabel = null!;
    private Label _toolsSelectionLabel = null!;
    private ComboBox _defaultPresetComboBox = null!;
    private ListBox _powerPlanList = null!;
    private Label _shaderCacheLabel = null!;
    private Label _profileCountLabel = null!;
    private Label _qualityLabel = null!;
    private Label _footerLabel = null!;
    private CheckBox _switchPowerPlanCheckBox = null!;
    private CheckBox _boostGamePriorityCheckBox = null!;
    private CheckBox _lowerBackgroundCheckBox = null!;
    private CheckBox _trimBackgroundMemoryCheckBox = null!;
    private CheckBox _closeBackgroundAppsCheckBox = null!;
    private MetricCard _boostStatusCard = null!;
    private MetricCard _compatibilityCard = null!;
    private MetricCard _overlayCard = null!;
    private MetricCard _impactCard = null!;
    private MetricCard _deltaCard = null!;
    private BoostGaugeControl _boostGauge = null!;
    private PerformanceOverlayForm? _overlayForm;
    private TelemetrySnapshot _latestTelemetry = new();
    private bool _isSyncingBoostControls;
    private bool _startupReady;
    private bool _startupFallbackMode;

    public MainForm()
    {
        _settings = _settingsService.Load();
        _presentMonFpsService = new PresentMonFpsService(_logger);
        _powerPlanService = new PowerPlanService(_logger);
        _timerResolutionService = new TimerResolutionService(_logger);
        _registryBoostService   = new WindowsBoostRegistryService(_logger);
        _boostCoordinator = new BoostCoordinator(_antiCheatCompatibilityService, _logger, _powerPlanService, _priorityService, _processService, _recoveryStateService, _timerResolutionService, _registryBoostService);
        _boostCoordinator.ConfigureTweaks(_settings.EnableTimerResolution, _settings.EnableMmcss, _settings.DisableGameDvr);
        _detectionService = new GameDetectionService(() => _settings.Profiles.Where(static profile => profile.AutoBoost).ToList(), _processService, _logger);

        try
        {
            _logger.Log("MainForm constructor: InitializeWindow");
            InitializeWindow();
            _logger.Log("MainForm constructor: BuildUi");
            BuildUi();
            _logger.Log("MainForm constructor: WireEvents");
            WireEvents();
            _logger.Log("MainForm constructor: BindProfiles");
            BindProfiles();
            _logger.Log("MainForm constructor complete.");
        }
        catch (Exception ex)
        {
            _logger.Log($"MainForm constructor failed: {ex}");
            BuildStartupFallbackUi(ex.Message);
        }
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_startupFallbackMode)
        {
            return;
        }

        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.Log($"Startup failed: {ex.Message}");
            MessageBox.Show(this, $"CloudFrame could not finish starting: {ex.Message}", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _telemetryTimer.Stop();
        _presentMonFpsService.Dispose();
        _systemTelemetryService.Dispose();
        _overlayForm?.Close();
        _overlayForm?.Dispose();
        _timerResolutionService.Dispose();
        _settingsService.Save(_settings);
        _detectionService.Dispose();
        _boostCoordinator.RestoreCurrentAsync("CloudFrame closed, restoring system state.").GetAwaiter().GetResult();
        _boostCoordinator.Dispose();
        base.OnFormClosing(e);
    }

    private void InitializeWindow()
    {
        Text = "CloudFrame";
        MinimumSize = new Size(1200, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.Canvas;
        Font = AppTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
        Size = new Size(
            Math.Max(MinimumSize.Width, Math.Min(workingArea.Width - 72, 1460)),
            Math.Max(MinimumSize.Height, Math.Min(workingArea.Height - 72, 940)));

        if (workingArea.Width < 1380 || workingArea.Height < 860)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(18, 8),
            Font = AppTheme.CaptionFont(9.5f),
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary
        };

        tabs.TabPages.Add(BuildSafeTab("Dashboard", BuildDashboardTab));
        tabs.TabPages.Add(BuildSafeTab("Profiles", BuildProfilesTab));
        tabs.TabPages.Add(BuildSafeTab("Tools", BuildToolsTab));

        _footerLabel = new Label
        {
            Text = "made with ♡ - Cloud",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 10, 16, 14),
            AutoSize = true
        };

        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(_footerLabel, 0, 1);
        Controls.Add(root);
    }

    private TabPage BuildSafeTab(string title, Func<TabPage> builder)
    {
        try
        {
            _logger.Log($"Building {title} tab.");
            return builder();
        }
        catch (Exception ex)
        {
            _logger.Log($"{title} tab failed to build: {ex}");
            return BuildErrorTab(title, ex.Message);
        }
    }

    private TabPage BuildErrorTab(string title, string message)
    {
        var tab = new TabPage(title)
        {
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24),
            BackColor = AppTheme.Canvas
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = $"{title} failed to load",
            AutoSize = true,
            Font = AppTheme.TitleFont(16f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(960, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 0, 0)
        }, 0, 1);

        tab.Controls.Add(layout);
        return tab;
    }

    private void BuildStartupFallbackUi(string message)
    {
        _startupFallbackMode = true;
        Controls.Clear();
        InitializeWindow();

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(28),
            BackColor = AppTheme.Canvas
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "CloudFrame recovered from a startup issue",
            AutoSize = true,
            Font = AppTheme.TitleFont(18f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);

        panel.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 12, 0, 0)
        }, 0, 1);

        panel.Controls.Add(new Label
        {
            Text = "Check frameboost.log in the CloudFrame data folder for the full startup trace.",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 18, 0, 0)
        }, 0, 2);

        Controls.Add(panel);
    }

    private TabPage BuildDashboardTab()
    {
        var tab = new TabPage("Dashboard");
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12),
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hero = new HeroPanel();
        var heroLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = AppTheme.Canvas
        };
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var heroText = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.Canvas
        };
        heroText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heroText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heroText.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        heroText.Controls.Add(new Label
        {
            Text = "CloudFrame",
            AutoSize = true,
            Font = AppTheme.TitleFont(24f),
            ForeColor = Color.White
        }, 0, 0);
        var heroSummaryLabel = new Label
        {
            Text = "Profile-first boosting, anti-cheat aware compatibility, and a sleek live overlay built for real-world game testing.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Font = AppTheme.BodyFont(10.5f),
            ForeColor = Color.FromArgb(232, 250, 252, 255),
            Margin = new Padding(0, 10, 0, 8)
        };
        heroText.Controls.Add(heroSummaryLabel, 0, 1);
        heroText.Controls.Add(new Label
        {
            Text = "Power mode changes are reversible. High-risk tweaks stay off the table.",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.2f),
            ForeColor = Color.FromArgb(217, 244, 246, 248)
        }, 0, 2);

        var heroButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.Canvas
        };
        var heroBoostButton = AppTheme.CreateButton("Boost Now", primary: true, width: 164);
        heroBoostButton.Click += async (_, _) => await UniversalBoostNowAsync();
        var heroStartButton = AppTheme.CreateButton("Scan Running Games", width: 164);
        heroStartButton.Click += (_, _) => ManualScanNow();
        heroButtons.Controls.Add(heroBoostButton);
        heroButtons.Controls.Add(heroStartButton);

        var heroMascot = CreateHeroMascot();

        heroLayout.Controls.Add(heroText, 0, 0);
        if (heroMascot is not null)
        {
            heroLayout.Controls.Add(heroMascot, 1, 0);
        }

        heroLayout.Controls.Add(heroButtons, 2, 0);
        hero.Controls.Add(heroLayout);

        // ── Live stat rings ───────────────────────────────────────────────────
        _cpuRing = new LiveStatRing
        {
            Caption      = "CPU",
            Unit         = "%",
            MaxValue     = 100f,
            UseTrafficLight = true,
            Margin       = new Padding(0, 0, 16, 0)
        };
        _gpuRing = new LiveStatRing
        {
            Caption      = "GPU",
            Unit         = "%",
            MaxValue     = 100f,
            UseTrafficLight = true,
            AccentColor  = Color.FromArgb(168, 85, 247),
            Margin       = new Padding(0, 0, 16, 0)
        };
        _gpuRing.UseTrafficLight = true;
        _gpuRing.AccentColor     = Color.FromArgb(168, 85, 247);
        _fpsRing = new LiveStatRing
        {
            Caption      = "FPS",
            Unit         = "fps",
            MaxValue     = 300f,
            UseTrafficLight = false,
            AccentColor  = AppTheme.AccentStrong,
            Margin       = new Padding(0, 0, 0, 0)
        };

        var metricRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 12),
            BackColor = AppTheme.Canvas
        };
        _boostStatusCard = new MetricCard();
        _compatibilityCard = new MetricCard();
        _overlayCard = new MetricCard();
        _impactCard = new MetricCard();
        _deltaCard = new MetricCard();
        metricRow.Controls.Add(_boostStatusCard);
        metricRow.Controls.Add(_compatibilityCard);
        metricRow.Controls.Add(_overlayCard);
        metricRow.Controls.Add(_impactCard);
        metricRow.Controls.Add(_deltaCard);
        metricRow.Controls.Add(_cpuRing);
        metricRow.Controls.Add(_gpuRing);
        metricRow.Controls.Add(_fpsRing);

        var gaugePanel = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 12),
            InnerPadding = new Padding(16)
        };
        _boostGauge = new BoostGaugeControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        _boostGauge.Caption = "Boost gauge";
        _boostGauge.Detail = "Preset-driven estimate of tuning strength and scope.";
        gaugePanel.Controls.Add(_boostGauge);

        var controlPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 12),
            InnerPadding = new Padding(16)
        };
        var controlLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.Surface
        };
        controlLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controlLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controlLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _boostControlScopeLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        };
        _boostControlHintLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 12)
        };

        var controlToggleFlow = new FlowLayoutPanel
        {
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            WrapContents  = true,
            Dock          = DockStyle.Top,
            Margin        = new Padding(0),
            Padding       = new Padding(0),
            BackColor     = AppTheme.Surface
        };

        _switchPowerPlanChip = new ToggleChip { Text = "Switch power plan" };
        _switchPowerPlanChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();
        _switchPowerPlanCheckBox = new CheckBox { Visible = false };  // kept for compat, chips are the UI

        _boostGamePriorityChip = new ToggleChip { Text = "Raise game priority" };
        _boostGamePriorityChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();
        _boostGamePriorityCheckBox = new CheckBox { Visible = false };

        _lowerBackgroundChip = new ToggleChip { Text = "Lower background apps" };
        _lowerBackgroundChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();
        _lowerBackgroundCheckBox = new CheckBox { Visible = false };

        _trimBackgroundMemoryChip = new ToggleChip { Text = "Trim background memory" };
        _trimBackgroundMemoryChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();
        _trimBackgroundMemoryCheckBox = new CheckBox { Visible = false };

        _closeBackgroundAppsChip = new ToggleChip { Text = "Gracefully close listed apps" };
        _closeBackgroundAppsChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();
        _closeBackgroundAppsCheckBox = new CheckBox { Visible = false };

        _memoryPriorityChip = new ToggleChip { Text = "Lower memory priority" };
        _memoryPriorityChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();

        _ecoQosChip = new ToggleChip { Text = "EcoQoS background apps" };
        _ecoQosChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();

        _maintenanceChip = new ToggleChip { Text = "Keep maintenance running" };
        _maintenanceChip.CheckedChanged += (_, _) => ApplyDashboardBoostControlChanges();

        controlToggleFlow.Controls.Add(_switchPowerPlanChip);
        controlToggleFlow.Controls.Add(_boostGamePriorityChip);
        controlToggleFlow.Controls.Add(_lowerBackgroundChip);
        controlToggleFlow.Controls.Add(_trimBackgroundMemoryChip);
        controlToggleFlow.Controls.Add(_memoryPriorityChip);
        controlToggleFlow.Controls.Add(_ecoQosChip);
        controlToggleFlow.Controls.Add(_maintenanceChip);
        controlToggleFlow.Controls.Add(_closeBackgroundAppsChip);

        controlLayout.Controls.Add(_boostControlScopeLabel, 0, 0);
        controlLayout.Controls.Add(_boostControlHintLabel, 0, 1);
        controlLayout.Controls.Add(controlToggleFlow, 0, 2);
        controlPanel.Controls.Add(controlLayout);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = AppTheme.Canvas
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _monitoringLabel = new Label { AutoSize = true, Padding = new Padding(0, 8, 18, 8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(10f) };
        _sessionLabel = new Label { AutoSize = true, Padding = new Padding(0, 8, 18, 8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(10f) };
        _elevationLabel = new Label { AutoSize = true, Padding = new Padding(0, 8, 18, 8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(10f) };
        _selectedProfileLabel = new Label { AutoSize = true, Padding = new Padding(0, 8, 18, 8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.AccentSoft, Font = AppTheme.CaptionFont(10f) };
        _presetSummaryLabel = new Label { AutoSize = true, Padding = new Padding(0, 8, 18, 8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.AccentSoft, Font = AppTheme.CaptionFont(10f) };

        _overlayToggleCheckBox = new CheckBox
        {
            Text = "Live overlay",
            AutoSize = true,
            Checked = true,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CaptionFont(10f),
            Padding = new Padding(0, 7, 18, 7)
        };
        _overlayToggleCheckBox.CheckedChanged += (_, _) => ToggleOverlay(_overlayToggleCheckBox.Checked);

        _overlayPositionComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Margin = new Padding(0, 4, 18, 4)
        });
        _overlayPositionComboBox.Items.Add("Top Left");
        _overlayPositionComboBox.Items.Add("Top Right");
        _overlayPositionComboBox.Items.Add("Bottom Left");
        _overlayPositionComboBox.Items.Add("Bottom Right");
        _overlayPositionComboBox.SelectedIndex = _settings.OverlayPosition switch
        {
            OverlayPosition.TopRight    => 1,
            OverlayPosition.BottomLeft  => 2,
            OverlayPosition.BottomRight => 3,
            _                           => 0
        };
        _overlayPositionComboBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.OverlayPosition = _overlayPositionComboBox.SelectedIndex switch
            {
                1 => OverlayPosition.TopRight,
                2 => OverlayPosition.BottomLeft,
                3 => OverlayPosition.BottomRight,
                _ => OverlayPosition.TopLeft
            };
            SaveSettings();
            RecreateOverlay();
        };

        _defaultPresetComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 168,
            Margin = new Padding(0, 2, 10, 0)
        });
        foreach (var preset in Enum.GetValues<BoostPresetOption>())
        {
            _defaultPresetComboBox.Items.Add(preset);
        }

        var defaultPresetIndex = _defaultPresetComboBox.Items.IndexOf(_settings.DefaultBoostPreset);
        _defaultPresetComboBox.SelectedIndex = defaultPresetIndex >= 0 ? defaultPresetIndex : 0;
        _defaultPresetComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_defaultPresetComboBox.SelectedItem is BoostPresetOption preset)
            {
                _settings.DefaultBoostPreset = preset;
                SaveSettings();
                UpdateStatusLabels();
                UpdateDashboardCards();
            }
        };

        var quickBoostButton = AppTheme.CreateButton("Boost Now", primary: true, width: 132);
        quickBoostButton.Click += async (_, _) => await UniversalBoostNowAsync();

        var startButton = AppTheme.CreateButton("Scan Running Games", width: 164);
        startButton.Click += (_, _) => ManualScanNow();

        var stopButton = AppTheme.CreateButton("Clear Matches", width: 132);
        stopButton.Click += (_, _) => ClearDetectedMatches();

        var refreshButton = AppTheme.CreateButton("Boost Selected Profile", width: 168);
        refreshButton.Click += async (_, _) => await BoostSelectedProfileAsync();

        var boostButton = AppTheme.CreateButton("Boost Selected Match", width: 160);
        boostButton.Click += async (_, _) => await BoostSelectedGameAsync();

        var maintenanceButton = AppTheme.CreateButton("Re-run Boost Pass", width: 156);
        maintenanceButton.Click += async (_, _) => await RunMaintenancePassNowAsync();

        var restoreButton = AppTheme.CreateButton("Restore Now", width: 126);
        restoreButton.Click += async (_, _) => await RestoreNowAsync();

        var infoFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0, 0, 0, 6)
        };
        infoFlow.Controls.Add(_monitoringLabel);
        infoFlow.Controls.Add(_sessionLabel);
        infoFlow.Controls.Add(_elevationLabel);
        infoFlow.Controls.Add(_selectedProfileLabel);
        infoFlow.Controls.Add(_presetSummaryLabel);
        infoFlow.Controls.Add(_overlayToggleCheckBox);
        infoFlow.Controls.Add(_overlayPositionComboBox);

        var buttonFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas
        };
        buttonFlow.Controls.Add(new Label
        {
            Text = "Boost preset",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 10, 8, 0),
            Margin = new Padding(0, 0, 0, 0)
        });
        buttonFlow.Controls.Add(_defaultPresetComboBox);
        buttonFlow.Controls.Add(quickBoostButton);
        buttonFlow.Controls.Add(startButton);
        buttonFlow.Controls.Add(stopButton);
        buttonFlow.Controls.Add(refreshButton);
        buttonFlow.Controls.Add(boostButton);
        buttonFlow.Controls.Add(maintenanceButton);
        buttonFlow.Controls.Add(restoreButton);

        statusPanel.Controls.Add(infoFlow, 0, 0);
        statusPanel.Controls.Add(buttonFlow, 0, 1);

        _detectedFilterTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Top });
        _detectedFilterTextBox.PlaceholderText = "Filter running matches by profile, process, path, or anti-cheat status";
        _detectedFilterTextBox.Margin = new Padding(0, 0, 0, 10);
        _detectedFilterTextBox.TextChanged += (_, _) => RefreshDetectedGrid();

        _detectedGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        AppTheme.StyleGrid(_detectedGrid);
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Profile", Width = 180 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "Process", Width = 140 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", Width = 70 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 200 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Executable", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _detectedGrid.CellDoubleClick += async (_, _) => await BoostSelectedGameAsync();

        _logTextBox = AppTheme.StyleTextBox(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        }, multiline: true);

        var detectedPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        detectedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detectedPanel.Controls.Add(_detectedFilterTextBox, 0, 0);
        detectedPanel.Controls.Add(_detectedGrid, 0, 1);

        var detectedGroup = new GroupBox
        {
            Text = "Running Matches",
            Dock = DockStyle.Fill,
            Font = AppTheme.CaptionFont(10f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Canvas
        };
        detectedGroup.Controls.Add(detectedPanel);

        var logGroup = new GroupBox
        {
            Text = "Activity Log",
            Dock = DockStyle.Fill,
            Font = AppTheme.CaptionFont(10f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Canvas
        };
        logGroup.Controls.Add(_logTextBox);

        root.Controls.Add(hero, 0, 0);
        root.Controls.Add(metricRow, 0, 1);
        root.Controls.Add(gaugePanel, 0, 2);
        root.Controls.Add(controlPanel, 0, 3);
        root.Controls.Add(statusPanel, 0, 4);
        root.Controls.Add(detectedGroup, 0, 5);
        root.Controls.Add(logGroup, 0, 6);

        void syncDashboardLayout()
        {
            var availableWidth = Math.Max(760, scrollHost.ClientSize.Width - 8);
            root.MaximumSize = new Size(availableWidth, 0);
            root.Width = availableWidth;

            var heroTextBudget = availableWidth - heroButtons.Width - (heroMascot?.Width ?? 0) - 140;
            heroSummaryLabel.MaximumSize = new Size(Math.Max(320, heroTextBudget), 0);
        }

        scrollHost.Resize += (_, _) => syncDashboardLayout();
        syncDashboardLayout();

        scrollHost.Controls.Add(root);
        tab.Controls.Add(scrollHost);
        return tab;
    }

    private static Control? CreateHeroMascot()
    {
        var mascotImage = BrandAssets.GetDashboardWatermark();
        if (mascotImage is null)
        {
            return null;
        }

        var mascotBox = new PictureBox
        {
            Image = mascotImage,
            Size = new Size(118, 118),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(12, 0, 18, 0),
            Anchor = AnchorStyles.Right
        };

        var host = new Panel
        {
            Width = 136,
            Height = 118,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0),
            Dock = DockStyle.Fill
        };
        mascotBox.Location = new Point(Math.Max(0, host.Width - mascotBox.Width), Math.Max(0, host.Height - mascotBox.Height));
        host.Controls.Add(mascotBox);
        host.Resize += (_, _) =>
        {
            mascotBox.Location = new Point(
                Math.Max(0, host.ClientSize.Width - mascotBox.Width),
                Math.Max(0, host.ClientSize.Height - mascotBox.Height));
        };

        return host;
    }

    private TabPage BuildProfilesTab()
    {
        var tab = new TabPage("Profiles");
        tab.BackColor = AppTheme.Canvas;
        tab.ForeColor = AppTheme.TextPrimary;
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 320
        };
        split.BackColor = AppTheme.Canvas;

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            BackColor = AppTheme.Canvas
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _profilesList = AppTheme.StyleListBox(new ListBox
        {
            Dock = DockStyle.Fill
        });
        _profilesList.DoubleClick += (_, _) => EditSelectedProfile();
        _profilesList.SelectedIndexChanged += (_, _) =>
        {
            UpdateStatusLabels();
            if (!_startupReady)
            {
                return;
            }

            UpdateFpsTrackingTarget();
            ScheduleTelemetryRefresh();
        };

        var profileButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        var addButton = AppTheme.CreateButton("Add Profile", primary: true, width: 126);
        addButton.Click += (_, _) => AddProfile();

        var editButton = AppTheme.CreateButton("Edit", width: 100);
        editButton.Click += (_, _) => EditSelectedProfile();

        var removeButton = AppTheme.CreateButton("Remove", width: 110);
        removeButton.Click += (_, _) => RemoveSelectedProfile();

        var launchButton = AppTheme.CreateButton("Launch Game", width: 126);
        launchButton.Click += (_, _) => LaunchSelectedProfile();

        var openFolderButton = AppTheme.CreateButton("Open Folder", width: 120);
        openFolderButton.Click += (_, _) => OpenSelectedProfileFolder();

        profileButtons.Controls.Add(addButton);
        profileButtons.Controls.Add(editButton);
        profileButtons.Controls.Add(removeButton);
        profileButtons.Controls.Add(launchButton);
        profileButtons.Controls.Add(openFolderButton);

        _profileCountLabel = AppTheme.CreateBodyLabel("No profiles configured yet.");
        _profileCountLabel.Margin = new Padding(0, 10, 0, 0);

        leftPanel.Controls.Add(_profilesList, 0, 0);
        leftPanel.Controls.Add(profileButtons, 0, 1);
        leftPanel.Controls.Add(_profileCountLabel, 0, 2);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ── Boost preset cards with spotlight + tilt effects ─────────────────
        var presetHeader = new Label
        {
            Text = "Boost Preset",
            AutoSize = true,
            Font = AppTheme.TitleFont(15f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 6)
        };
        var presetSub = new Label
        {
            Text = "Pick the tuning intensity for the selected profile. Hover the cards to preview them.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 14)
        };

        _balancedCard = new BoostPresetCard
        {
            Preset      = BoostPresetOption.Balanced,
            PresetTitle = "Balanced",
            Description = "Light tuning for long sessions. Power plan + gentle background priority — nothing aggressive.",
            ImpactScore = 45,
            AccentColor = Color.FromArgb(37, 99, 235)
        };
        _performanceCard = new BoostPresetCard
        {
            Preset      = BoostPresetOption.Performance,
            PresetTitle = "Performance",
            Description = "Full background priority tuning, EcoQoS, and memory trim. The sweet spot.",
            ImpactScore = 68,
            AccentColor = AppTheme.AccentStrong
        };
        _maxFpsCard = new BoostPresetCard
        {
            Preset      = BoostPresetOption.MaxFps,
            PresetTitle = "Max FPS",
            Description = "Everything cranked. EcoQoS + aggressive priority + trim. Use for benchmarks.",
            ImpactScore = 90,
            AccentColor = Color.FromArgb(217, 119, 6)
        };

        void SelectPreset(BoostPresetOption preset)
        {
            var profile = SelectedProfile;
            if (profile is not null)
            {
                profile.BoostPreset = preset;
                SaveSettings();
                _logger.Log($"Set preset for '{profile.Name}' to {preset}.");
            }
            else
            {
                _settings.DefaultBoostPreset = preset;
                SaveSettings();
            }
            SyncPresetCardSelection();
            UpdateStatusLabels();
            UpdateDashboardCards();
        }

        _balancedCard.CardSelected    += (_, _) => SelectPreset(BoostPresetOption.Balanced);
        _performanceCard.CardSelected += (_, _) => SelectPreset(BoostPresetOption.Performance);
        _maxFpsCard.CardSelected      += (_, _) => SelectPreset(BoostPresetOption.MaxFps);
        SyncPresetCardSelection();

        var cardRack = new FlowLayoutPanel
        {
            AutoSize      = true,
            WrapContents  = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor     = AppTheme.Canvas,
            Margin        = new Padding(0)
        };
        cardRack.Controls.Add(_balancedCard);
        cardRack.Controls.Add(_performanceCard);
        cardRack.Controls.Add(_maxFpsCard);

        var presetSection = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 3,
            AutoSize    = true,
            BackColor   = AppTheme.Canvas
        };
        presetSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        presetSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        presetSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        presetSection.Controls.Add(presetHeader, 0, 0);
        presetSection.Controls.Add(presetSub,    0, 1);
        presetSection.Controls.Add(cardRack,     0, 2);

        // ── Profile-first workflow hint card ─────────────────────────────────
        var card = new CardPanel
        {
            Dock        = DockStyle.Fill,
            FillColor   = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            Padding     = new Padding(22),
            Height      = 200
        };
        var cardLayout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 3,
            BackColor   = AppTheme.Surface
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardLayout.Controls.Add(new Label
        {
            Text      = "Profile-first workflow",
            AutoSize  = true,
            Font      = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        cardLayout.Controls.Add(new Label
        {
            Text          = "Double-click a profile or press Edit to open its full settings. Bind the currently running process if the game launches through a bootstrapper.",
            AutoSize      = true,
            MaximumSize   = new Size(520, 0),
            Font          = AppTheme.BodyFont(10f),
            ForeColor     = AppTheme.TextSecondary,
            Margin        = new Padding(0, 8, 0, 12)
        }, 0, 1);
        var quickEditButton = AppTheme.CreateButton("Edit Selected Profile", primary: true, width: 176);
        quickEditButton.Click += (_, _) => EditSelectedProfile();
        cardLayout.Controls.Add(quickEditButton, 0, 2);
        card.Controls.Add(cardLayout);

        // ── Profile snapshot card ────────────────────────────────────────────────
        _profileSnapshotCard = new ProfileSnapshotCard
        {
            Margin = new Padding(0, 14, 0, 0)
        };
        _profileSnapshotCard.SetProfile(null);
        _profilesList.SelectedIndexChanged += (_, _) => _profileSnapshotCard?.SetProfile(SelectedProfile);

        rightPanel.Controls.Add(presetSection, 0, 0);
        rightPanel.Controls.Add(_profileSnapshotCard, 0, 1);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);

        tab.Controls.Add(split);
        return tab;
    }

    private TabPage BuildToolsTab()
    {
        var tab = new TabPage("Tools");
        tab.BackColor = AppTheme.Canvas;
        tab.ForeColor = AppTheme.TextPrimary;
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(16),
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttonFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            BackColor = AppTheme.Canvas
        };

        var refreshPlansButton = AppTheme.CreateButton("Refresh Power Plans", primary: true, width: 156);
        refreshPlansButton.Click += async (_, _) => await LoadPowerPlansAsync();

        var cleanShaderCacheButton = AppTheme.CreateButton("Clean DirectX Shader Cache", width: 190);
        cleanShaderCacheButton.Click += (_, _) => CleanShaderCache();

        var trimMemoryButton = AppTheme.CreateButton("Trim Safe Background Memory", width: 210);
        trimMemoryButton.Click += (_, _) => TrimSafeBackgroundMemoryNow();

        var closeBackgroundButton = AppTheme.CreateButton("Gracefully Close Listed Apps", width: 220);
        closeBackgroundButton.Click += (_, _) => GracefullyCloseBackgroundAppsNow();

        var rerunMaintenanceButton = AppTheme.CreateButton("Re-run Active Boost Pass", width: 196);
        rerunMaintenanceButton.Click += async (_, _) => await RunMaintenancePassNowAsync();

        var openDataButton = AppTheme.CreateButton("Open Data Folder", width: 144);
        openDataButton.Click += (_, _) => OpenDataFolder();

        var openLogButton = AppTheme.CreateButton("Open Log File", width: 132);
        openLogButton.Click += (_, _) => OpenLogFile();

        var clearLogButton = AppTheme.CreateButton("Clear Log", width: 112);
        clearLogButton.Click += (_, _) => ClearLog();

        buttonFlow.Controls.Add(refreshPlansButton);
        buttonFlow.Controls.Add(cleanShaderCacheButton);
        buttonFlow.Controls.Add(trimMemoryButton);
        buttonFlow.Controls.Add(closeBackgroundButton);
        buttonFlow.Controls.Add(rerunMaintenanceButton);
        buttonFlow.Controls.Add(openDataButton);
        buttonFlow.Controls.Add(openLogButton);
        buttonFlow.Controls.Add(clearLogButton);

        var safetyCard = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };
        var safetyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = AppTheme.Surface
        };
        safetyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        safetyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        safetyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        safetyLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _toolsSelectionLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(15f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        };
        _safeTargetsLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1050, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 10)
        };

        _powerPlanList = AppTheme.StyleListBox(new ListBox
        {
            Dock = DockStyle.Top,
            Height = 260
        });

        _shaderCacheLabel = new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 8),
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CaptionFont(10f),
            BackColor = AppTheme.Surface
        };

        _qualityLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1050, 0),
            Text = "CloudFrame keeps cleanup safe: no service killing, no forced kills, no Defender or virtualization changes, and no undocumented RAM purges. Memory cleanup only trims working sets on non-protected user apps from your profile list.",
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface
        };

        safetyLayout.Controls.Add(_toolsSelectionLabel, 0, 0);
        safetyLayout.Controls.Add(_safeTargetsLabel, 0, 1);
        safetyLayout.Controls.Add(_shaderCacheLabel, 0, 2);
        safetyLayout.Controls.Add(_qualityLabel, 0, 3);
        safetyCard.Controls.Add(safetyLayout);

        root.Controls.Add(buttonFlow, 0, 0);
        root.Controls.Add(BuildOverlaySettingsCard(), 0, 1);
        root.Controls.Add(BuildSessionDefaultsCard(), 0, 2);
        root.Controls.Add(BuildBoostTweaksCard(), 0, 3);
        root.Controls.Add(safetyCard, 0, 4);
        root.Controls.Add(AppTheme.CreateSectionTitle("Available Power Plans"), 0, 5);
        root.Controls.Add(_powerPlanList, 0, 6);

        scrollHost.Controls.Add(root);
        tab.Controls.Add(scrollHost);
        return tab;
    }

    private CardPanel BuildOverlaySettingsCard()
    {
        var card = new CardPanel
        {
            Dock         = DockStyle.Top,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor    = AppTheme.Surface,
            BorderColor  = AppTheme.Border,
            CornerRadius = 18,
            Margin       = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 4, BackColor = AppTheme.Surface, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Overlay Appearance", AutoSize = true,
            Font = AppTheme.TitleFont(14f), ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Changes apply immediately — no restart needed.",
            AutoSize = true, Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        // ── Style + metrics row ──────────────────────────────────────────────
        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, BackColor = AppTheme.Surface, Margin = new Padding(0, 0, 0, 10)
        };

        var styleLabel = new Label
        {
            Text = "Style:", AutoSize = true,
            ForeColor = AppTheme.TextSecondary, Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 7, 6, 0)
        };
        _overlayStyleComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 110,
            Margin = new Padding(0, 4, 18, 4)
        });
        _overlayStyleComboBox.Items.Add("Card");
        _overlayStyleComboBox.Items.Add("Minimal");
        _overlayStyleComboBox.SelectedIndex = _settings.OverlayStyle == OverlayStyle.Minimal ? 1 : 0;
        _overlayStyleComboBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.OverlayStyle = _overlayStyleComboBox.SelectedIndex == 1 ? OverlayStyle.Minimal : OverlayStyle.Card;
            SaveSettings(); RecreateOverlay();
        };

        _overlayShowFpsCheckBox = new CheckBox
        {
            Text = "FPS", AutoSize = true, Checked = _settings.OverlayShowFps,
            ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 7, 14, 7)
        };
        _overlayShowFpsCheckBox.CheckedChanged += (_, _) =>
        { _settings.OverlayShowFps = _overlayShowFpsCheckBox.Checked; SaveSettings(); RecreateOverlay(); };

        _overlayShowCpuCheckBox = new CheckBox
        {
            Text = "CPU", AutoSize = true, Checked = _settings.OverlayShowCpu,
            ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 7, 14, 7)
        };
        _overlayShowCpuCheckBox.CheckedChanged += (_, _) =>
        { _settings.OverlayShowCpu = _overlayShowCpuCheckBox.Checked; SaveSettings(); RecreateOverlay(); };

        _overlayShowGpuCheckBox = new CheckBox
        {
            Text = "GPU", AutoSize = true, Checked = _settings.OverlayShowGpu,
            ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 7, 22, 7)
        };
        _overlayShowGpuCheckBox.CheckedChanged += (_, _) =>
        { _settings.OverlayShowGpu = _overlayShowGpuCheckBox.Checked; SaveSettings(); RecreateOverlay(); };

        // Accent colour swatch + picker
        _overlayAccentPreview = new Panel
        {
            Width = 22, Height = 22,
            BackColor = Color.FromArgb(_settings.OverlayAccentArgb),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 6, 6, 4),
            Cursor = Cursors.Hand
        };
        var accentBtn = AppTheme.CreateButton("Accent Colour", width: 128);
        accentBtn.Margin = new Padding(0, 4, 0, 4);
        accentBtn.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = Color.FromArgb(_settings.OverlayAccentArgb), FullOpen = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _settings.OverlayAccentArgb = dlg.Color.ToArgb();
            _overlayAccentPreview.BackColor = dlg.Color;
            SaveSettings();
            RecreateOverlay();
        };

        row.Controls.Add(styleLabel);
        row.Controls.Add(_overlayStyleComboBox);
        row.Controls.Add(new Label
        {
            Text = "Show:", AutoSize = true, ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9.5f), Padding = new Padding(0, 7, 6, 0)
        });
        row.Controls.Add(_overlayShowFpsCheckBox);
        row.Controls.Add(_overlayShowCpuCheckBox);
        row.Controls.Add(_overlayShowGpuCheckBox);
        row.Controls.Add(_overlayAccentPreview);
        row.Controls.Add(accentBtn);

        layout.Controls.Add(row, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private CardPanel BuildSessionDefaultsCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.Surface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Session Defaults",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "These shape the universal Boost Now flow and the overall launch experience. Profile-specific settings still win when you edit a saved game.",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };

        CheckBox MakeDefaultToggle(string label, bool initial, Action<bool> onChange, string tip)
        {
            var cb = new CheckBox
            {
                Text = label,
                AutoSize = true,
                Checked = initial,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CaptionFont(9.5f),
                Padding = new Padding(0, 6, 18, 6)
            };
            var tt = new ToolTip();
            tt.SetToolTip(cb, tip);
            cb.CheckedChanged += (_, _) =>
            {
                onChange(cb.Checked);
                SaveSettings();
                SyncBoostControlState();
                UpdateToolsSelectionInfo();
                UpdateDashboardCards();
            };
            return cb;
        }

        flow.Controls.Add(MakeDefaultToggle(
            "Check for updates on launch",
            _settings.EnableUpdateChecks,
            v => _settings.EnableUpdateChecks = v,
            "Shows an update prompt when a newer CloudFrame release is available."));

        flow.Controls.Add(MakeDefaultToggle(
            "Lower memory priority by default",
            _settings.UniversalUseBackgroundMemoryPriority,
            v => _settings.UniversalUseBackgroundMemoryPriority = v,
            "Makes listed background apps give up RAM pressure sooner during the universal boost flow."));

        flow.Controls.Add(MakeDefaultToggle(
            "Use EcoQoS by default",
            _settings.UniversalUseBackgroundEcoQos,
            v => _settings.UniversalUseBackgroundEcoQos = v,
            "Steers listed background work toward efficiency mode on stronger presets instead of competing with the game for top-performance cores."));

        flow.Controls.Add(MakeDefaultToggle(
            "Keep maintenance running",
            _settings.UniversalEnableRecurringMaintenance,
            v => _settings.UniversalEnableRecurringMaintenance = v,
            "Reapplies trims and background tuning every few seconds while the boost session is active."));

        layout.Controls.Add(flow, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private CardPanel BuildBoostTweaksCard()
    {
        var card = new CardPanel
        {
            Dock         = DockStyle.Top,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor    = AppTheme.Surface,
            BorderColor  = AppTheme.Border,
            CornerRadius = 18,
            Margin       = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 3, BackColor = AppTheme.Surface, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Boost Engine", AutoSize = true,
            Font = AppTheme.TitleFont(14f), ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "System-side tuning applied during boost sessions and reversed on restore. These are the deeper engine switches behind the presets.",
            AutoSize = true, MaximumSize = new Size(900, 0), Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary, Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, BackColor = AppTheme.Surface
        };

        CheckBox MakeTweakToggle(string label, bool initial, Action<bool> onChange, string tip)
        {
            var cb = new CheckBox
            {
                Text = label, AutoSize = true, Checked = initial,
                ForeColor = AppTheme.TextPrimary, Font = AppTheme.CaptionFont(9.5f),
                Padding = new Padding(0, 6, 18, 6)
            };
            var tt = new ToolTip();
            tt.SetToolTip(cb, tip);
            cb.CheckedChanged += (_, _) =>
            {
                onChange(cb.Checked);
                SaveSettings();
                _boostCoordinator.ConfigureTweaks(_settings.EnableTimerResolution, _settings.EnableMmcss, _settings.DisableGameDvr);
            };
            return cb;
        }

        row.Controls.Add(MakeTweakToggle(
            "1 ms timer resolution",
            _settings.EnableTimerResolution,
            v => _settings.EnableTimerResolution = v,
            "Calls timeBeginPeriod(1) during boost. Reduces sleep granularity for smoother frame pacing. Used by Process Lasso, Razer Cortex, and NVIDIA App."));

        row.Controls.Add(MakeTweakToggle(
            "MMCSS CPU priority (requires admin)",
            _settings.EnableMmcss,
            v => _settings.EnableMmcss = v,
            "Sets NetworkThrottlingIndex=0xFFFFFFFF and SystemResponsiveness=0 in the MMCSS registry key. Gives games full MMCSS CPU quota. Requires elevation; silently skipped otherwise."));

        row.Controls.Add(MakeTweakToggle(
            "Disable Game DVR capture hook",
            _settings.DisableGameDvr,
            v => _settings.DisableGameDvr = v,
            "Disables Xbox Game Bar's hidden DirectX capture hook (GameDVR_Enabled=0, AppCaptureEnabled=0). Removes a background DX overhead layer. Reversed on restore."));

        layout.Controls.Add(row, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private void WireEvents()
    {
        _logger.MessageLogged += HandleLogMessage;
        _detectionService.SnapshotUpdated += HandleSnapshotUpdated;
        _boostCoordinator.ActiveSessionChanged += (_, session) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(() => HandleActiveSessionChanged(session));
            }
        };
        _telemetryTimer.Tick += (_, _) => ScheduleTelemetryRefresh();
    }

    private async Task InitializeAsync()
    {
        _logger.Log("CloudFrame main window shown.");
        UpdateStatusLabels();
        await LoadPowerPlansAsync();
        UpdateShaderCacheLabel();
        await _boostCoordinator.RecoverPendingSessionAsync(_settings.Profiles);
        ToggleOverlay(_overlayToggleCheckBox.Checked);

        await Task.Delay(700);
        if (IsDisposed || Disposing)
        {
            return;
        }

        _startupReady = true;
        UpdateFpsTrackingTarget();
        _telemetryTimer.Start();
        ScheduleTelemetryRefresh();
        _logger.Log("CloudFrame startup warmup complete.");
    }

    private async Task LoadPowerPlansAsync()
    {
        _powerPlans.Clear();
        _powerPlans.AddRange(await _powerPlanService.GetPlansAsync());
        _powerPlanList.DataSource = null;
        _powerPlanList.DataSource = _powerPlans;
        UpdateStatusLabels();
    }

    private void BindProfiles()
    {
        var selectedProfileId = SelectedProfile?.Id;
        _profilesList.DataSource = null;
        _profilesList.DisplayMember = nameof(GameProfile.Name);
        // Prepend the sentinel so users can always return to universal-boost mode
        var items = new List<GameProfile> { NoneProfile };
        items.AddRange(_settings.Profiles);
        _profilesList.DataSource = items;
        _profileCountLabel.Text = _settings.Profiles.Count switch
        {
            0 => "No profiles configured yet. Add a game executable to get started.",
            1 => "1 profile configured and ready for testing.",
            _ => $"{_settings.Profiles.Count} profiles configured."
        };

        if (_profilesList.Items.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedProfileId) && selectedProfileId != "__none__")
        {
            var matchingIndex = items.FindIndex(profile => string.Equals(profile.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase));
            if (matchingIndex >= 0 && matchingIndex < _profilesList.Items.Count)
            {
                _profilesList.SelectedIndex = matchingIndex;
                return;
            }
        }

        _profilesList.SelectedIndex = 0; // default to None
    }

    private static readonly GameProfile NoneProfile = new()
    {
        Id = "__none__",
        Name = "— None (Universal Boost) —"
    };

    private GameProfile? SelectedProfile
    {
        get
        {
            var profile = _profilesList.SelectedItem as GameProfile;
            return profile?.Id == "__none__" ? null : profile;
        }
    }

    private void AddProfile()
    {
        var profile = new GameProfile
        {
            Name = "New Game Profile"
        };

        if (!EditProfile(profile))
        {
            return;
        }

        _settings.Profiles.Add(profile);
        SaveSettings();
        BindProfiles();
        SelectProfile(profile);
        _logger.Log($"Added profile for '{profile.Name}'.");
    }

    private void EditSelectedProfile()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            MessageBox.Show(this, "Select a profile first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!EditProfile(profile))
        {
            return;
        }

        SaveSettings();
        BindProfiles();
        SelectProfile(profile);
        _logger.Log($"Saved profile '{profile.Name}'.");
    }

    private bool EditProfile(GameProfile profile)
    {
        using var dialog = new ProfileEditorForm(profile, _powerPlans, GetRunningProcessEntries);
        var result = dialog.ShowDialog(this);
        if (result != DialogResult.OK)
        {
            return false;
        }

        if (_settings.Profiles.Any(existing =>
                !ReferenceEquals(existing, profile)
                && string.Equals(existing.ExecutablePath, dialog.WorkingCopy.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "That executable already has a profile.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        return true;
    }

    private void SelectProfile(GameProfile? profile)
    {
        if (profile is null || _profilesList.Items.Count == 0)
        {
            _profilesList.SelectedIndex = 0; // fall back to None sentinel
            return;
        }

        var matchingIndex = (_profilesList.DataSource as List<GameProfile>)
            ?.FindIndex(item => string.Equals(item.Id, profile.Id, StringComparison.OrdinalIgnoreCase)) ?? -1;
        if (matchingIndex >= 0 && matchingIndex < _profilesList.Items.Count)
        {
            _profilesList.SelectedIndex = matchingIndex;
        }
    }

    private void RemoveSelectedProfile()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        var result = MessageBox.Show(this, $"Remove the profile '{profile.Name}'?", "CloudFrame", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _settings.Profiles.Remove(profile);
        BindProfiles();
        SaveSettings();
        _logger.Log($"Removed profile '{profile.Name}'.");
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }

    private void ManualScanNow()
    {
        if (_settings.Profiles.Count == 0)
        {
            _logger.Log("No game profiles are configured yet. Add a profile first from the Profiles tab.");
            return;
        }

        _logger.Log("Manual scan started.");
        _detectionService.ForceScan();
        UpdateStatusLabels();
    }

    private void ClearDetectedMatches()
    {
        _detectedGames.Clear();
        _logger.Log("Cleared running matches from the dashboard.");
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private async Task BoostSelectedGameAsync()
    {
        if (_detectedGrid.CurrentRow?.Tag is not DetectedGame detectedGame)
        {
            MessageBox.Show(this, "Select a running match first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = await _boostCoordinator.ApplyBoostAsync(detectedGame.Profile, detectedGame.ProcessId, false);
        _logger.Log(result.Message);
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private async Task BoostSelectedProfileAsync()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            MessageBox.Show(this, "Select a saved profile from the Profiles tab first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var matches = FindMatchesForProfile(profile);
        if (matches.Count == 0)
        {
            MessageBox.Show(this, "No running process matched that profile. Launch the game first, then try again.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (matches.Count == 1)
        {
            var result = await _boostCoordinator.ApplyBoostAsync(profile, matches[0].ProcessId, false);
            _logger.Log(result.Message);
            UpdateStatusLabels();
            RefreshDetectedGrid();
            return;
        }

        using var picker = new RunningProcessPickerForm(matches.Select(match => new RunningProcessEntry
        {
            ProcessId = match.ProcessId,
            ProcessName = match.ProcessName,
            WindowTitle = match.ProcessName,
            ExecutablePath = match.ExecutablePath ?? profile.ExecutablePath
        }).ToList());

        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedEntry is null)
        {
            return;
        }

        var selected = matches.FirstOrDefault(match => match.ProcessId == picker.SelectedEntry.ProcessId);
        if (selected is null)
        {
            return;
        }

        var selectedResult = await _boostCoordinator.ApplyBoostAsync(profile, selected.ProcessId, false);
        _logger.Log(selectedResult.Message);
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private async Task RestoreNowAsync()
    {
        await _boostCoordinator.RestoreCurrentAsync("Manual restore requested.");
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private async Task RunMaintenancePassNowAsync()
    {
        var result = await _boostCoordinator.RunMaintenancePassNowAsync();
        _logger.Log(result.Message);
        UpdateStatusLabels();
        ScheduleTelemetryRefresh();
    }

    private async Task UniversalBoostNowAsync()
    {
        var universalProfile = CreateUniversalBoostProfile();
        var result = await _boostCoordinator.ApplyPreLaunchBoostAsync(universalProfile);
        _logger.Log(result.Message);
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private GameProfile CreateUniversalBoostProfile()
    {
        var preferredPlan = ChooseUniversalBoostPlan();
        var preset = _defaultPresetComboBox.SelectedItem is BoostPresetOption selectedPreset
            ? selectedPreset
            : _settings.DefaultBoostPreset;
        return new GameProfile
        {
            Id = "cloudframe-universal-boost",
            Name = "Universal Boost",
            AutoBoost = false,
            SwitchPowerPlan = _settings.UniversalSwitchPowerPlan,
            PreferredPowerPlanGuid = preferredPlan?.Guid,
            PreferredPowerPlanName = preferredPlan?.Name,
            BoostPreset = preset,
            BoostGamePriority = false,
            GamePriority = ProcessPriorityOption.High,
            LowerBackgroundProcesses = _settings.UniversalLowerBackgroundProcesses,
            TrimBackgroundMemory = _settings.UniversalTrimBackgroundMemory,
            CloseBackgroundAppsGracefully = _settings.UniversalCloseBackgroundAppsGracefully,
            UseBackgroundMemoryPriority = _settings.UniversalUseBackgroundMemoryPriority,
            UseBackgroundEcoQos = _settings.UniversalUseBackgroundEcoQos,
            EnableRecurringMaintenance = _settings.UniversalEnableRecurringMaintenance,
            BackgroundProcessesRaw = "Discord,steamwebhelper,chrome,msedge,firefox,opera,EpicGamesLauncher,GalaxyClient,Overwolf,OneDrive,RiotClientServices"
        };
    }

    private PowerPlanInfo? ChooseUniversalBoostPlan()
    {
        return _powerPlans.FirstOrDefault(plan => plan.Name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
            ?? _powerPlans.FirstOrDefault(plan => plan.Name.Contains("High performance", StringComparison.OrdinalIgnoreCase))
            ?? _powerPlans.FirstOrDefault(plan => plan.IsActive)
            ?? _powerPlans.FirstOrDefault();
    }

    private static CheckBox CreateDashboardToggle(string text)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CaptionFont(9.5f),
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 18, 10)
        };
    }

    private GameProfile CreateBoostControlPreviewProfile()
    {
        var selectedProfile = SelectedProfile;
        if (selectedProfile is not null)
        {
            return selectedProfile;
        }

        return CreateUniversalBoostProfile();
    }

    private void SyncPresetCardSelection()
    {
        if (_balancedCard is null || _performanceCard is null || _maxFpsCard is null) return;
        var activePreset = SelectedProfile?.BoostPreset ?? _settings.DefaultBoostPreset;
        _balancedCard.IsSelected    = activePreset == BoostPresetOption.Balanced;
        _performanceCard.IsSelected = activePreset == BoostPresetOption.Performance;
        _maxFpsCard.IsSelected      = activePreset == BoostPresetOption.MaxFps;
        _balancedCard.Invalidate();
        _performanceCard.Invalidate();
        _maxFpsCard.Invalidate();
    }

    private void SyncBoostControlState()
    {
        if (_switchPowerPlanCheckBox is null)
        {
            return;
        }

        var profile = CreateBoostControlPreviewProfile();
        var selectedProfile = SelectedProfile;

        _isSyncingBoostControls = true;
        try
        {
            _boostControlScopeLabel.Text = selectedProfile is null
                ? "Quick boost controls"
                : $"Boost controls for {selectedProfile.Name}";
            _boostControlHintLabel.Text = selectedProfile is null
                ? "No saved profile is selected, so these controls shape the universal Boost Now flow. Select a profile to edit its exact live boost actions from the dashboard."
                : "These toggles save directly into the selected profile so CloudFrame only touches the settings you allow.";

            _switchPowerPlanChip.Checked       = profile.SwitchPowerPlan;
            _boostGamePriorityChip.Checked     = profile.BoostGamePriority;
            _boostGamePriorityChip.Enabled     = selectedProfile is not null;
            _lowerBackgroundChip.Checked       = profile.LowerBackgroundProcesses;
            _trimBackgroundMemoryChip.Checked  = profile.TrimBackgroundMemory;
            _memoryPriorityChip.Checked        = profile.UseBackgroundMemoryPriority;
            _ecoQosChip.Checked                = profile.UseBackgroundEcoQos;
            _maintenanceChip.Checked           = profile.EnableRecurringMaintenance;
            _closeBackgroundAppsChip.Checked   = profile.CloseBackgroundAppsGracefully;
            // Keep hidden checkboxes in sync for any code that reads them
            _switchPowerPlanCheckBox.Checked       = profile.SwitchPowerPlan;
            _boostGamePriorityCheckBox.Checked     = profile.BoostGamePriority;
            _lowerBackgroundCheckBox.Checked       = profile.LowerBackgroundProcesses;
            _trimBackgroundMemoryCheckBox.Checked  = profile.TrimBackgroundMemory;
            _closeBackgroundAppsCheckBox.Checked   = profile.CloseBackgroundAppsGracefully;
        }
        finally
        {
            _isSyncingBoostControls = false;
        }
    }

    private void ApplyDashboardBoostControlChanges()
    {
        if (_isSyncingBoostControls || _switchPowerPlanChip is null)
        {
            return;
        }

        var selectedProfile = SelectedProfile;
        if (selectedProfile is not null)
        {
            selectedProfile.SwitchPowerPlan = _switchPowerPlanChip.Checked;
            selectedProfile.BoostGamePriority = _boostGamePriorityChip.Checked;
            selectedProfile.LowerBackgroundProcesses = _lowerBackgroundChip.Checked;
            selectedProfile.TrimBackgroundMemory = _trimBackgroundMemoryChip.Checked;
            selectedProfile.UseBackgroundMemoryPriority = _memoryPriorityChip.Checked;
            selectedProfile.UseBackgroundEcoQos = _ecoQosChip.Checked;
            selectedProfile.EnableRecurringMaintenance = _maintenanceChip.Checked;
            selectedProfile.CloseBackgroundAppsGracefully = _closeBackgroundAppsChip.Checked;
            SaveSettings();
            _logger.Log($"Updated boost controls for '{selectedProfile.Name}'.");
        }
        else
        {
            _settings.UniversalSwitchPowerPlan = _switchPowerPlanChip.Checked;
            _settings.UniversalLowerBackgroundProcesses = _lowerBackgroundChip.Checked;
            _settings.UniversalTrimBackgroundMemory = _trimBackgroundMemoryChip.Checked;
            _settings.UniversalUseBackgroundMemoryPriority = _memoryPriorityChip.Checked;
            _settings.UniversalUseBackgroundEcoQos = _ecoQosChip.Checked;
            _settings.UniversalEnableRecurringMaintenance = _maintenanceChip.Checked;
            _settings.UniversalCloseBackgroundAppsGracefully = _closeBackgroundAppsChip.Checked;
            SaveSettings();
            _logger.Log("Updated universal boost controls.");
        }

        UpdateToolsSelectionInfo();
        UpdateDashboardCards();
    }

    private void UpdateToolsSelectionInfo()
    {
        if (_toolsSelectionLabel is null || _safeTargetsLabel is null)
        {
            return;
        }

        var profile = CreateBoostControlPreviewProfile();
        var targets = profile.GetBackgroundProcessNames().Take(12).ToList();
        var suffix = profile.GetBackgroundProcessNames().Count() > targets.Count ? " ..." : string.Empty;

        _toolsSelectionLabel.Text = SelectedProfile is null
            ? "Tools are targeting the universal boost list"
            : $"Tools are targeting {profile.Name}";
        _safeTargetsLabel.Text = targets.Count == 0
            ? "No background targets are configured yet. Add app names in the selected profile if you want CloudFrame to trim or close them safely."
            : $"Listed safe targets: {string.Join(", ", targets)}{suffix}";
    }

    private IEnumerable<Process> EnumerateSafeBackgroundProcesses(GameProfile profile)
    {
        foreach (var processName in profile.GetBackgroundProcessNames())
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                if (process.HasExited || _processService.IsProtectedProcess(process))
                {
                    process.Dispose();
                    continue;
                }

                if (process.ProcessName.Equals("CloudFrame", StringComparison.OrdinalIgnoreCase))
                {
                    process.Dispose();
                    continue;
                }

                yield return process;
            }
        }
    }

    private void TrimSafeBackgroundMemoryNow()
    {
        var profile = CreateBoostControlPreviewProfile();
        var trimmedCount = 0;

        foreach (var process in EnumerateSafeBackgroundProcesses(profile))
        {
            using (process)
            {
                if (_processService.TryTrimWorkingSet(process, out _))
                {
                    trimmedCount++;
                }
            }
        }

        _logger.Log(trimmedCount == 0
            ? $"No listed background apps were available to trim for '{profile.Name}'."
            : $"Trimmed memory working sets on {trimmedCount} listed background app instance(s) for '{profile.Name}'.");
        ScheduleTelemetryRefresh();
    }

    private void GracefullyCloseBackgroundAppsNow()
    {
        var profile = CreateBoostControlPreviewProfile();
        var result = MessageBox.Show(
            this,
            $"Ask the listed background apps for '{profile.Name}' to close gracefully? CloudFrame will not force-kill anything.",
            "CloudFrame",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        var closedCount = 0;
        foreach (var process in EnumerateSafeBackgroundProcesses(profile))
        {
            using (process)
            {
                if (_processService.TryCloseGracefully(process, TimeSpan.FromSeconds(2), out _))
                {
                    closedCount++;
                }
            }
        }

        _logger.Log(closedCount == 0
            ? $"No listed background apps closed for '{profile.Name}'."
            : $"Gracefully closed {closedCount} listed background app instance(s) for '{profile.Name}'.");
        ManualScanNow();
    }

    private void HandleLogMessage(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => HandleLogMessage(message));
            return;
        }

        if (_logTextBox is null || _logTextBox.IsDisposed)
        {
            return;
        }

        _logTextBox.AppendText(message + Environment.NewLine);
    }

    private void HandleSnapshotUpdated(object? sender, IReadOnlyList<DetectedGame> games)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => HandleSnapshotUpdated(sender, games));
            return;
        }

        _detectedGames.Clear();
        _detectedGames.AddRange(games);
        UpdateFpsTrackingTarget();
        RefreshDetectedGrid();
        ScheduleTelemetryRefresh();
    }

    private void RefreshDetectedGrid()
    {
        _detectedGrid.Rows.Clear();

        var filter = _detectedFilterTextBox.Text.Trim();
        var matches = _detectedGames.Where(game => MatchesDetectedGameFilter(game, filter));

        foreach (var game in matches)
        {
            var status = _boostCoordinator.ActiveSession?.RecoveryState.GameProcessId == game.ProcessId
                ? BuildSessionStatus(_boostCoordinator.ActiveSession)
                : "Detected / ready";

            var rowIndex = _detectedGrid.Rows.Add(
                game.Profile.Name,
                game.ProcessName,
                game.ProcessId,
                status,
                game.ExecutablePath ?? game.Profile.ExecutablePath);

            _detectedGrid.Rows[rowIndex].Tag = game;
        }
    }

    private void UpdateStatusLabels()
    {
        var session = _boostCoordinator.ActiveSession;
        var selectedProfile = SelectedProfile;
        _monitoringLabel.Text = session?.RecoveryState.IsPreLaunchBoost == true
            ? "Detection: Universal boost armed"
            : "Detection: Manual scan only";
        _sessionLabel.Text = session is null
            ? "Active Session: None"
            : session.RecoveryState.IsPreLaunchBoost
                ? "Active Session: Universal boost"
                : session.AntiCheatStatus.UseCompatibilityMode
                    ? $"Active Session: {session.Profile.Name} (Compatibility mode: {session.AntiCheatStatus.DisplayName})"
                    : $"Active Session: {session.Profile.Name} (PID {session.RecoveryState.GameProcessId})";
        _elevationLabel.Text = IsElevated()
            ? "Permissions: Elevated"
            : "Permissions: Standard user";
        _selectedProfileLabel.Text = selectedProfile is null
            ? "Selected Profile: None"
            : $"Selected Profile: {selectedProfile.Name}";
        _presetSummaryLabel.Text = selectedProfile is null
            ? $"Boost Preset: {_settings.DefaultBoostPreset}"
            : $"Boost Preset: {selectedProfile.BoostPreset}";
        SyncBoostControlState();
        SyncPresetCardSelection();
        UpdateToolsSelectionInfo();
        UpdateDashboardCards();
    }

    private void UpdateFpsTrackingTarget()
    {
        if (!_startupReady)
        {
            return;
        }

        // Keep PresentMon running in global capture mode whenever the overlay
        // toggle is on — this enables foreground-window FPS even without a boost.
        var overlayOn = _overlayToggleCheckBox?.Checked == true;
        _presentMonFpsService.SetForegroundMode(overlayOn);

        var session = _boostCoordinator.ActiveSession;
        if (session is not null && !session.RecoveryState.IsPreLaunchBoost)
        {
            _presentMonFpsService.SetTarget(
                session.RecoveryState.GameProcessId,
                session.Profile.Name,
                session.Profile.ExecutableName,
                session.AntiCheatStatus.UseCompatibilityMode,
                session.AntiCheatStatus.UseCompatibilityMode
                    ? $"{session.AntiCheatStatus.DisplayName} compatibility mode keeps FPS capture disabled for safety."
                    : null);
            return;
        }

        _presentMonFpsService.SetTarget(null, null, null, false, null);
    }

    private void UpdateShaderCacheLabel()
    {
        var sizeBytes = _shaderCacheService.GetCacheSizeBytes();
        _shaderCacheLabel.Text = $"DirectX shader cache: {_shaderCacheService.CacheDirectory} ({FormatBytes(sizeBytes)})";
    }

    private void CleanShaderCache()
    {
        if (_boostCoordinator.ActiveSession is not null)
        {
            MessageBox.Show(this, "Restore the active gaming session before cleaning shader cache.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            this,
            "Delete files in the DirectX shader cache folder? Do this only while no game is running.",
            "CloudFrame",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        var deletedFiles = _shaderCacheService.ClearCache();
        _logger.Log($"Cleaned {deletedFiles} DirectX shader cache files.");
        UpdateShaderCacheLabel();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private void OpenDataFolder()
    {
        AppPaths.EnsureDataDirectory();
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.DataDirectory,
            UseShellExecute = true
        });
    }

    private void OpenLogFile()
    {
        AppPaths.EnsureDataDirectory();
        if (!File.Exists(AppPaths.LogPath))
        {
            File.WriteAllText(AppPaths.LogPath, string.Empty);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.LogPath,
            UseShellExecute = true
        });
    }

    private void ClearLog()
    {
        _logger.Clear();
        _logTextBox.Clear();
        _logger.Log("Activity log cleared.");
    }

    private void LaunchSelectedProfile()
    {
        var profile = SelectedProfile;
        if (profile is null || string.IsNullOrWhiteSpace(profile.ExecutablePath) || !File.Exists(profile.ExecutablePath))
        {
            MessageBox.Show(this, "Select a saved profile with a valid executable first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = profile.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(profile.ExecutablePath),
            UseShellExecute = true
        });

        _logger.Log($"Launched '{profile.Name}' from CloudFrame.");
    }

    private void OpenSelectedProfileFolder()
    {
        var profile = SelectedProfile;
        if (profile is null || string.IsNullOrWhiteSpace(profile.ExecutablePath) || !File.Exists(profile.ExecutablePath))
        {
            MessageBox.Show(this, "Select a saved profile with a valid executable first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var directory = Path.GetDirectoryName(profile.ExecutablePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private static OpenFileDialog CreateExecutablePicker()
    {
        return new OpenFileDialog
        {
            Filter = "Windows executables (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Choose a game executable"
        };
    }

    private List<RunningProcessEntry> GetRunningProcessEntries()
    {
        var entries = new List<RunningProcessEntry>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited || _processService.IsProtectedProcess(process))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    {
                        continue;
                    }

                    if (!_processService.TryGetExecutablePath(process, out var executablePath) || string.IsNullOrWhiteSpace(executablePath))
                    {
                        continue;
                    }

                    entries.Add(new RunningProcessEntry
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        WindowTitle = process.MainWindowTitle,
                        ExecutablePath = executablePath
                    });
                }
                catch
                {
                }
            }
        }

        return entries
            .DistinctBy(static item => item.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item.ProcessName)
            .ToList();
    }

    private List<DetectedGame> FindMatchesForProfile(GameProfile profile)
    {
        var matches = new List<DetectedGame>();
        var executableName = profile.ExecutableName;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    if (!_processService.TryGetExecutablePath(process, out var executablePath) || string.IsNullOrWhiteSpace(executablePath))
                    {
                        continue;
                    }

                    var pathMatch = string.Equals(executablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                    var nameMatch = string.Equals(process.ProcessName, executableName, StringComparison.OrdinalIgnoreCase);
                    if (!pathMatch && !nameMatch)
                    {
                        continue;
                    }

                    matches.Add(new DetectedGame
                    {
                        Profile = profile,
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        ExecutablePath = executablePath
                    });
                }
                catch
                {
                }
            }
        }

        return matches;
    }

    private DetectedGame? FindLiveMatchForProfile(GameProfile profile)
    {
        var executableName = profile.ExecutableName;
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return null;
        }

        foreach (var process in Process.GetProcessesByName(executableName))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    if (_processService.TryGetExecutablePath(process, out var executablePath) && !string.IsNullOrWhiteSpace(executablePath))
                    {
                        var pathMatch = string.Equals(executablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                        var nameMatch = string.Equals(
                            Path.GetFileNameWithoutExtension(executablePath),
                            executableName,
                            StringComparison.OrdinalIgnoreCase);

                        if (!pathMatch && !nameMatch)
                        {
                            continue;
                        }

                        return new DetectedGame
                        {
                            Profile = profile,
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            ExecutablePath = executablePath
                        };
                    }

                    return new DetectedGame
                    {
                        Profile = profile,
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        ExecutablePath = profile.ExecutablePath
                    };
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string BuildSessionStatus(ActiveBoostSession? session)
    {
        if (session is null)
        {
            return "Detected";
        }

        if (session.RecoveryState.IsPreLaunchBoost)
        {
            return "Universal boost active";
        }

        return session.AntiCheatStatus.UseCompatibilityMode
            ? $"Boost active / {session.AntiCheatStatus.DisplayName} compatibility"
            : "Boost active";
    }

    private static bool MatchesDetectedGameFilter(DetectedGame game, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return game.Profile.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || game.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (game.ExecutablePath?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || game.Profile.ExecutablePath.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleActiveSessionChanged(ActiveBoostSession? session)
    {
        UpdateStatusLabels();
        RefreshDetectedGrid();

        UpdateFpsTrackingTarget();
        ScheduleTelemetryRefresh();
        ToggleOverlay(_overlayToggleCheckBox.Checked);
    }

    private void ScheduleTelemetryRefresh()
    {
        if (!_startupReady || IsDisposed || Disposing)
        {
            return;
        }

        _ = RefreshTelemetryAsync();
    }

    private FpsTrackingTarget? _lastKnownForegroundTarget;
    private DateTimeOffset _fpsBecameNullAt = DateTimeOffset.MinValue;

    private async Task RefreshTelemetryAsync()
    {
        if (!_telemetryRefreshGate.Wait(0))
        {
            return;
        }

        try
        {
            UpdateFpsTrackingTarget();
            var session = _boostCoordinator.ActiveSession;
            var hasLiveSession = session is not null && !session.RecoveryState.IsPreLaunchBoost;
            var cpuTask = Task.Run(() => _systemTelemetryService.GetCpuPercent());
            var gpuTask = Task.Run(() => _systemTelemetryService.GetGpuPercent());
            await Task.WhenAll(cpuTask, gpuTask).ConfigureAwait(false);

            // When no boost session is active, fall back to tracking whichever
            // non-system window currently has focus so the overlay always works.
            // We keep the last known valid target so clicking the desktop or
            // taskbar on another monitor doesn't instantly hide the overlay.
            if (!hasLiveSession && _overlayToggleCheckBox?.Checked == true)
            {
                var resolved = ResolveForegroundFpsTarget();
                if (resolved is not null)
                {
                    _lastKnownForegroundTarget = resolved;
                    _fpsBecameNullAt = DateTimeOffset.MinValue; // reset stale timer when we resolve a real window
                }

                // Track how long FPS has been absent; only evict the last known target
                // once it has been consistently null for 10+ seconds (game closed/crashed).
                if (_presentMonFpsService.LatestFps is null)
                {
                    if (_fpsBecameNullAt == DateTimeOffset.MinValue)
                        _fpsBecameNullAt = DateTimeOffset.UtcNow;

                    if (_lastKnownForegroundTarget is not null
                        && DateTimeOffset.UtcNow - _fpsBecameNullAt > TimeSpan.FromSeconds(10))
                    {
                        _lastKnownForegroundTarget = null;
                    }
                }
                else
                {
                    _fpsBecameNullAt = DateTimeOffset.MinValue;
                }
            }
            else if (hasLiveSession)
            {
                _lastKnownForegroundTarget = null;
                _fpsBecameNullAt = DateTimeOffset.MinValue;
            }

            var telemetry = new TelemetrySnapshot
            {
                CpuPercent = cpuTask.Result,
                GpuPercent = gpuTask.Result,
                FramesPerSecond = _presentMonFpsService.LatestFps,
                FpsStatus = _presentMonFpsService.Status,
                TargetProcessId = hasLiveSession
                    ? session!.RecoveryState.GameProcessId
                    : _lastKnownForegroundTarget?.ProcessId,
                TargetProcessName = hasLiveSession
                    ? session!.Profile.Name
                    : _lastKnownForegroundTarget?.DisplayName,
                CompatibilityMode = hasLiveSession && session!.AntiCheatStatus.UseCompatibilityMode,
                CompatibilityLabel = hasLiveSession
                    ? session!.AntiCheatStatus.UseCompatibilityMode
                        ? $"{session.AntiCheatStatus.DisplayName} compatibility mode active"
                        : null
                    : null
            };

            if (IsDisposed || Disposing)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(() => ApplyTelemetrySnapshot(telemetry));
                return;
            }

            ApplyTelemetrySnapshot(telemetry);
        }
        finally
        {
            _telemetryRefreshGate.Release();
        }
    }

    private FpsTrackingTarget? ResolveForegroundFpsTarget()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.HasExited || _processService.IsProtectedProcess(process) || process.ProcessName.Equals("CloudFrame", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!_processService.TryGetExecutablePath(process, out var executablePath) || string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            var title = GetWindowText(windowHandle);
            var matchedProfile = _settings.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(profile.ExecutableName, process.ProcessName, StringComparison.OrdinalIgnoreCase));

            return new FpsTrackingTarget
            {
                ProcessId = process.Id,
                ExecutablePath = executablePath,
                MatchName = matchedProfile?.ExecutableName ?? process.ProcessName,
                DisplayName = matchedProfile?.Name
                    ?? (!string.IsNullOrWhiteSpace(title) ? title : process.ProcessName)
            };
        }
        catch
        {
            return null;
        }
    }

    private void ApplyTelemetrySnapshot(TelemetrySnapshot telemetry)
    {
        _latestTelemetry = telemetry;
        _fpsComparisonTracker.Observe(telemetry, _boostCoordinator.ActiveSession);

        // Update live stat rings
        if (_cpuRing is not null)
            _cpuRing.Value = (float)telemetry.CpuPercent;
        if (_gpuRing is not null)
            _gpuRing.Value = (float)(telemetry.GpuPercent ?? 0);
        if (_fpsRing is not null)
        {
            if (telemetry.FramesPerSecond is double fps)
                _fpsRing.Value = (float)Math.Min(fps, 300);
            // else ring holds last value, which is fine
        }

        if (ShouldDisplayOverlay(telemetry))
        {
            var overlay = EnsureOverlayForm();
            overlay.UpdateSnapshot(_latestTelemetry);
            if (!overlay.Visible)
            {
                overlay.Show(this);
            }
        }
        else
        {
            _overlayForm?.Hide();
        }

        UpdateDashboardCards();
    }

    private void RefreshTelemetry() => ScheduleTelemetryRefresh();

    private void ToggleOverlay(bool enabled)
    {
        _presentMonFpsService.SetForegroundMode(enabled);
        if (!enabled)
        {
            _lastKnownForegroundTarget = null;
            _fpsBecameNullAt = DateTimeOffset.MinValue;
            _overlayForm?.Hide();
        }
        else
        {
            // Show immediately — PresentMon will start warming up and FPS will
            // populate within ~1.5s. Until then the overlay shows "-- FPS".
            var overlay = EnsureOverlayForm();
            overlay.UpdateSnapshot(_latestTelemetry);
            if (!overlay.Visible)
            {
                overlay.Show(this);
            }
            ScheduleTelemetryRefresh();
        }
        UpdateDashboardCards();
    }

    private PerformanceOverlayForm EnsureOverlayForm()
    {
        if (_overlayForm is not null && !_overlayForm.IsDisposed)
        {
            return _overlayForm;
        }

        _overlayForm = new PerformanceOverlayForm(_settings);
        return _overlayForm;
    }

    private void RecreateOverlay()
    {
        _overlayForm?.Hide();
        _overlayForm?.Dispose();
        _overlayForm = null;
        ToggleOverlay(_overlayToggleCheckBox.Checked);
    }

    private void UpdateDashboardCards()
    {
        if (_boostStatusCard is null || _compatibilityCard is null || _overlayCard is null || _impactCard is null || _deltaCard is null)
        {
            return;
        }

        var session = _boostCoordinator.ActiveSession;
        _boostStatusCard.SetContent(
            "BOOST STATUS",
            session is null ? "Idle" : session.RecoveryState.IsPreLaunchBoost ? "Primed" : "Live",
            session is null
                ? "Waiting for a profiled game launch."
                : session.RecoveryState.IsPreLaunchBoost
                    ? "Universal boost is active and ready for your next game."
                    : $"Boosting {session.Profile.Name}",
            session is null ? AppTheme.TextPrimary : AppTheme.Accent);

        var compatibilityText = session?.AntiCheatStatus.UseCompatibilityMode == true ? "Guarded" : "Normal";
        var compatibilityDetail = session?.AntiCheatStatus.UseCompatibilityMode == true
            ? $"{session.AntiCheatStatus.DisplayName} detected. Safe compatibility mode is active."
            : "No anti-cheat compatibility limits are active right now.";
        _compatibilityCard.SetContent(
            "COMPATIBILITY",
            compatibilityText,
            compatibilityDetail,
            session?.AntiCheatStatus.UseCompatibilityMode == true ? AppTheme.Warning : AppTheme.Success);

        var overlayArmed = _overlayToggleCheckBox?.Checked == true;
        var overlayActive = ShouldDisplayOverlay(_latestTelemetry);
        var overlayValue = !overlayArmed ? "Hidden" : "Visible";
        var overlayDetail = !overlayArmed
            ? "Toggle the live overlay back on anytime."
            : _latestTelemetry.FramesPerSecond is double fps
                ? $"{fps:0} FPS live, CPU {_latestTelemetry.CpuPercent:0}%, GPU {(_latestTelemetry.GpuPercent ?? 0):0}%"
                : _latestTelemetry.FpsStatus ?? "Tracking foreground window";
        _overlayCard.SetContent(
            "LIVE OVERLAY",
            overlayValue,
            overlayDetail,
            overlayActive ? AppTheme.AccentStrong : overlayArmed ? AppTheme.Accent : AppTheme.TextSecondary);

        var impactSource = session?.Profile ?? SelectedProfile ?? CreateUniversalBoostProfile();
        var impactScore = EstimateImpactScore(impactSource, session);
        _impactCard.SetContent(
            "BOOST PLAN",
            $"{impactScore}% scope",
            BuildImpactSummary(impactSource, session),
            impactScore >= 80 ? AppTheme.Success : impactScore >= 55 ? AppTheme.AccentStrong : AppTheme.Warning);

        var delta = _fpsComparisonTracker.GetSnapshot();
        _deltaCard.SetContent(
            "MEASURED DELTA",
            delta.Value,
            delta.Detail,
            delta.HasResult
                ? (delta.DeltaPercent ?? 0) >= 0 ? AppTheme.Success : AppTheme.Warning
                : AppTheme.AccentStrong);

        if (_boostGauge is not null)
        {
            _boostGauge.Value = Math.Clamp(impactScore, 0, 100);
            _boostGauge.Caption = session is null ? "Boost scope" : "Applied actions";
            _boostGauge.Detail = session is null
                ? BuildPlannedActionSummary(impactSource)
                : BuildAppliedActionSummary(session);
        }
    }

    private bool ShouldDisplayOverlay(TelemetrySnapshot snapshot)
    {
        // Show whenever the toggle is ticked — no boost session required.
        // FPS will show "--" briefly while PresentMon warms up, then populate.
        return _overlayToggleCheckBox?.Checked == true;
    }

    private static string BuildAppliedActionSummary(ActiveBoostSession session)
    {
        var parts = new List<string>();
        if (session.RecoveryState.PowerPlanChanged)
        {
            parts.Add("power plan");
        }

        if (session.RecoveryState.GamePriorityRaised)
        {
            parts.Add("game priority");
        }

        if (session.RecoveryState.BackgroundPriorityTunedCount > 0)
        {
            parts.Add($"{session.RecoveryState.BackgroundPriorityTunedCount} tuned");
        }

        if (session.RecoveryState.BackgroundMemoryPriorityCount > 0)
        {
            parts.Add($"{session.RecoveryState.BackgroundMemoryPriorityCount} mem-priority");
        }

        if (session.RecoveryState.BackgroundEcoQosCount > 0)
        {
            parts.Add($"{session.RecoveryState.BackgroundEcoQosCount} EcoQoS");
        }

        if (session.Profile.ShouldRunRecurringMaintenance())
        {
            parts.Add("maintenance");
        }

        if (session.RecoveryState.TrimmedProcessCount > 0)
        {
            parts.Add($"{session.RecoveryState.TrimmedProcessCount} trims");
        }

        if (session.RecoveryState.GracefullyClosedProcessCount > 0)
        {
            parts.Add($"{session.RecoveryState.GracefullyClosedProcessCount} closed");
        }

        return parts.Count == 0
            ? "Monitoring the session with safe settings."
            : string.Join(" · ", parts);
    }

    private static string BuildPlannedActionSummary(GameProfile profile)
    {
        var parts = new List<string>();
        if (profile.SwitchPowerPlan)
        {
            parts.Add("power plan");
        }

        if (profile.BoostGamePriority)
        {
            parts.Add("game priority");
        }

        if (profile.LowerBackgroundProcesses)
        {
            parts.Add("background priority");
        }

        if (profile.ShouldLowerBackgroundMemoryPriority())
        {
            parts.Add("memory priority");
        }

        if (profile.ShouldApplyBackgroundEcoQos())
        {
            parts.Add("EcoQoS");
        }

        if (profile.ShouldRunRecurringMaintenance())
        {
            parts.Add("maintenance");
        }

        if (profile.TrimBackgroundMemory)
        {
            parts.Add("memory trim");
        }

        if (profile.CloseBackgroundAppsGracefully)
        {
            parts.Add("graceful close");
        }

        return parts.Count == 0
            ? "No boost actions are enabled yet."
            : $"{string.Join(" · ", parts)} · {profile.GetBackgroundProcessNames().Count()} targets";
    }

    private static int EstimateImpactScore(GameProfile profile, ActiveBoostSession? session)
    {
        var score = profile.BoostPreset switch
        {
            BoostPresetOption.Balanced => 42,
            BoostPresetOption.Performance => 65,
            BoostPresetOption.MaxFps => 82,
            _ => 60
        };

        if (profile.SwitchPowerPlan)
        {
            score += 4;
        }

        if (profile.LowerBackgroundProcesses)
        {
            score += profile.BoostPreset == BoostPresetOption.MaxFps ? 8 : 5;
        }

        if (profile.BoostGamePriority)
        {
            score += profile.GamePriority switch
            {
                ProcessPriorityOption.High => 4,
                ProcessPriorityOption.AboveNormal => 2,
                _ => 0
            };
        }

        if (profile.TrimBackgroundMemory)
        {
            score += 5;
        }

        if (profile.ShouldLowerBackgroundMemoryPriority())
        {
            score += 5;
        }

        if (profile.ShouldApplyBackgroundEcoQos())
        {
            score += 4;
        }

        if (profile.ShouldRunRecurringMaintenance())
        {
            score += profile.BoostPreset == BoostPresetOption.MaxFps ? 6 : 3;
        }

        if (profile.CloseBackgroundAppsGracefully)
        {
            score += 7;
        }

        if (!profile.SwitchPowerPlan
            && !profile.BoostGamePriority
            && !profile.LowerBackgroundProcesses
            && !profile.ShouldLowerBackgroundMemoryPriority()
            && !profile.TrimBackgroundMemory
            && !profile.ShouldRunRecurringMaintenance()
            && !profile.CloseBackgroundAppsGracefully)
        {
            score = 0;
        }

        if (session?.AntiCheatStatus.UseCompatibilityMode == true)
        {
            score -= 12;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static string BuildImpactSummary(GameProfile profile, ActiveBoostSession? session)
    {
        var backgroundCount = profile.GetBackgroundProcessNames().Count();
        var parts = new List<string>
        {
            $"{profile.BoostPreset} preset",
            $"{backgroundCount} background targets",
            profile.SwitchPowerPlan ? "power plan" : "power unchanged",
            profile.BoostGamePriority ? $"priority {profile.GamePriority}" : "priority unchanged"
        };

        if (profile.TrimBackgroundMemory)
        {
            parts.Add("memory trim");
        }

        if (profile.ShouldLowerBackgroundMemoryPriority())
        {
            parts.Add("memory priority");
        }

        if (profile.ShouldApplyBackgroundEcoQos())
        {
            parts.Add("EcoQoS");
        }

        if (profile.ShouldRunRecurringMaintenance())
        {
            parts.Add("maintenance loop");
        }

        if (profile.CloseBackgroundAppsGracefully)
        {
            parts.Add("graceful close");
        }

        if (profile.PreferredPowerPlanName is { Length: > 0 } planName)
        {
            parts.Add(planName);
        }

        if (session?.AntiCheatStatus.UseCompatibilityMode == true)
        {
            parts.Add("compatibility-safe");
        }

        return string.Join(" · ", parts);
    }

    private static string GetWindowText(IntPtr windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(length + 1);
        return GetWindowText(windowHandle, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private sealed class FpsTrackingTarget
    {
        public required int ProcessId { get; init; }

        public required string ExecutablePath { get; init; }

        public required string MatchName { get; init; }

        public required string DisplayName { get; init; }
    }

}
