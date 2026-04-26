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
    private readonly ProfileTuningAdvisor _profileTuningAdvisor = new();
    private readonly UpdateCheckerService _updateCheckerService;
    private readonly UpdateInstallerService _updateInstallerService;
    private readonly IssueReportService _issueReportService = new();
    private readonly ProcessService _processService = new();
    private readonly GpuTechnologyAdvisorService _gpuTechnologyAdvisorService = new();
    private readonly WindowResolutionService _windowResolutionService = new();
    private readonly FrameGenPrototypeService _frameGenPrototypeService;
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
    private readonly List<RunningProcessEntry> _scannedProcesses = [];
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
    private CheckBox _minimizeToTrayCheckBox = null!;
    private ComboBox _overlayProfileComboBox = null!;
    private ComboBox _overlayPositionComboBox = null!;
    private ComboBox _overlayStyleComboBox = null!;
    private CheckBox _overlayShowFpsCheckBox = null!;
    private CheckBox _overlayShowCpuCheckBox = null!;
    private CheckBox _overlayShowGpuCheckBox = null!;
    private Panel   _overlayAccentPreview   = null!;
    private Panel   _overlayTextPreview     = null!;
    private Panel   _overlayBackgroundPreview = null!;
    private ComboBox _overlayFontComboBox   = null!;
    private ComboBox _themePresetComboBox   = null!;
    private ComboBox _resolutionProcessComboBox = null!;
    private ComboBox _resolutionWindowComboBox = null!;
    private ComboBox _resolutionPresetComboBox = null!;
    private ComboBox _frameGenCaptureComboBox = null!;
    private ComboBox _frameGenBackendComboBox = null!;
    private NumericUpDown _resolutionWidthInput = null!;
    private NumericUpDown _resolutionHeightInput = null!;
    private CheckBox _resolutionBorderlessCheckBox = null!;
    private CheckBox _frameGenEnableCheckBox = null!;
    private CheckBox _frameGenRequireBorderlessCheckBox = null!;
    private CheckBox _frameGenDisableOnAntiCheatCheckBox = null!;
    private CheckBox _frameGenLowLatencyCheckBox = null!;
    private Label _resolutionStatusLabel = null!;
    private Label _frameGenStatusLabel = null!;
    private Label _frameGenGpuSummaryLabel = null!;
    private Label _frameGenEligibilityLabel = null!;
    private Label _frameGenEligibilityDetailLabel = null!;
    private Label _monitoringLabel = null!;
    private Label _sessionLabel = null!;
    private Label _elevationLabel = null!;
    private Label _selectedProfileLabel = null!;
    private Label _presetSummaryLabel = null!;
    private Label _boostControlScopeLabel = null!;
    private Label _boostControlHintLabel = null!;
    private Label _safeTargetsLabel = null!;
    private Label _toolsSelectionLabel = null!;
    private Label _diagnosticsSummaryLabel = null!;
    private Label _presentMonReadyLabel = null!;
    private Label _gpuReadyLabel = null!;
    private Label _elevationReadyLabel = null!;
    private Label _overlayReadyLabel = null!;
    private Label _launcherHandoffLabel = null!;
    private Label _trayModeLabel = null!;
    private Label _sessionReportLabel = null!;
    private Label _sessionReportDetailLabel = null!;
    private Label _recommendationLabel = null!;
    private Label _recommendationDetailLabel = null!;
    private ComboBox _defaultPresetComboBox = null!;
    private ComboBox _monitoringModeComboBox = null!;
    private ListBox _powerPlanList = null!;
    private Label _shaderCacheLabel = null!;
    private Label _profileCountLabel = null!;
    private Label _qualityLabel = null!;
    private Label _footerLabel = null!;
    private TabControl _mainTabs = null!;
    private Label _experienceStatusTitleLabel = null!;
    private Label _experienceStatusDetailLabel = null!;
    private Label _experienceStatusContextLabel = null!;
    private Label _experienceVersionLabel = null!;
    private Label _toolsPreviewThemeLabel = null!;
    private Label _toolsPreviewOverlayLabel = null!;
    private Label _toolsPreviewMonitoringLabel = null!;
    private Label _toolsPreviewDefaultsLabel = null!;
    private Label _dashboardSummaryLabel = null!;
    private Label _dashboardSummaryDetailLabel = null!;
    private Label _dashboardScanSummaryLabel = null!;
    private Label _advisorLabel = null!;
    private Label _advisorDetailLabel = null!;
    private Label _performanceLabSummaryLabel = null!;
    private Label _performanceLabBaselineLabel = null!;
    private Label _performanceLabLiveLabel = null!;
    private Label _performanceLabPacingLabel = null!;
    private CheckBox _advancedViewCheckBox = null!;
    private Control _dashboardGaugePanel = null!;
    private Control _dashboardPerformanceLabPanel = null!;
    private Control _dashboardSessionReportPanel = null!;
    private Control _dashboardRecommendationPanel = null!;
    private Control _dashboardMatchesGroup = null!;
    private Control _dashboardLogGroup = null!;
    private readonly ToolTip _uiToolTip = new();
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
    private DashboardBackdropPanel? _dashboardBackdropPanel;
    private PerformanceOverlayForm? _overlayForm;
    private NotifyIcon? _trayIcon;
    private TelemetrySnapshot _latestTelemetry = new();
    private bool _isSyncingBoostControls;
    private bool _isSyncingOverlayStudio;
    private bool _profileStudioDirty = true;
    private bool _toolsUiDirty = true;
    private bool _dashboardUiDirty = true;
    private bool _startupReady;
    private bool _startupFallbackMode;
    private bool _restoringFromTray;
    private bool _forceExitRequested;
    private bool _isCheckingForUpdates;
    private bool _profileRefreshQueued;
    private bool _toolsRefreshQueued;
    private bool _dashboardRefreshQueued;
    private bool _tabSwitchInProgress;
    private bool _deferredWarmupStarted;
    private ActiveBoostSession? _lastLiveSession;
    private SessionReport? _lastSessionReport;
    private ProfileTuningRecommendation _currentRecommendation = ProfileTuningRecommendation.Empty;
    private int _telemetrySampleCounter;
    private double? _lastGpuTelemetry;
    private readonly Dictionary<string, Func<TabPage>> _deferredTabBuilders = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _experienceStatusTimer = new() { Interval = 4200 };
    private readonly System.Windows.Forms.Timer _deferredTabWarmupTimer = new() { Interval = 260 };
    private readonly Queue<string> _deferredTabWarmupQueue = new();
    private string? _transientExperienceMessage;
    private string? _transientExperienceContext;
    private bool _transientExperienceWarning;

    public MainForm()
    {
        _settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(_settings.GitHubRepository))
        {
            _settings.GitHubRepository = "CloudyCodez/CloudFrame";
        }
        ApplyMonitoringMode();
        AppTheme.ApplyPreset(_settings.ThemePreset);
        _presentMonFpsService = new PresentMonFpsService(_logger);
        _presentMonFpsService.SampleCaptured += HandlePresentMonSampleCaptured;
        _updateCheckerService = new UpdateCheckerService(_logger);
        _updateInstallerService = new UpdateInstallerService(_logger);
        _powerPlanService = new PowerPlanService(_logger);
        _timerResolutionService = new TimerResolutionService(_logger);
        _registryBoostService   = new WindowsBoostRegistryService(_logger);
        _frameGenPrototypeService = new FrameGenPrototypeService(_gpuTechnologyAdvisorService, _antiCheatCompatibilityService, _windowResolutionService);
        _boostCoordinator = new BoostCoordinator(_antiCheatCompatibilityService, _logger, _powerPlanService, _priorityService, _processService, _recoveryStateService, _timerResolutionService, _registryBoostService);
        _boostCoordinator.ConfigureTweaks(_settings.EnableTimerResolution, _settings.EnableMmcss, _settings.DisableGameDvr);
        _detectionService = new GameDetectionService(() => _settings.Profiles.Where(static profile => profile.AutoBoost).ToList(), _processService, _logger);
        _experienceStatusTimer.Tick += (_, _) =>
        {
            _experienceStatusTimer.Stop();
            _transientExperienceMessage = null;
            _transientExperienceContext = null;
            _transientExperienceWarning = false;
            UpdateExperienceStatusFromState();
        };
        _deferredTabWarmupTimer.Tick += (_, _) => WarmNextDeferredTab();

        try
        {
            _logger.Log("MainForm constructor: InitializeWindow");
            InitializeWindow();
            _logger.Log("MainForm constructor: BuildUi");
            BuildUi();
            InitializeTrayIcon();
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

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (_restoringFromTray && WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
            BringToFront();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_forceExitRequested)
        {
            var result = MessageBox.Show(
                this,
                "Would you like to close CloudFrame fully or send it to the tray?\r\n\r\nYes = close fully\r\nNo = exit to tray\r\nCancel = stay open",
                "Exit CloudFrame",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                SendToTray("CloudFrame was sent to the tray.");
                return;
            }

            _forceExitRequested = true;
        }

        _telemetryTimer.Stop();
        _presentMonFpsService.Dispose();
        _systemTelemetryService.Dispose();
        _overlayForm?.Close();
        _overlayForm?.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _timerResolutionService.Dispose();
        _settingsService.Save(_settings);
        _detectionService.Dispose();
        _boostCoordinator.RestoreCurrentAsync("CloudFrame closed, restoring system state.").GetAwaiter().GetResult();
        _boostCoordinator.Dispose();
        base.OnFormClosing(e);
    }

    private void InitializeTrayIcon()
    {
        var trayMenu = new ContextMenuStrip
        {
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.TextPrimary
        };

        var restoreItem = new ToolStripMenuItem("Open CloudFrame");
        restoreItem.Click += (_, _) => RestoreFromTray();
        var exitItem = new ToolStripMenuItem("Exit CloudFrame");
        exitItem.Click += (_, _) =>
        {
            _forceExitRequested = true;
            Close();
        };
        trayMenu.Items.Add(restoreItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Text = "CloudFrame",
            Visible = false,
            ContextMenuStrip = trayMenu,
            Icon = Icon ?? SystemIcons.Application
        };

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
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
            RowCount = 3,
            ColumnCount = 1,
            BackColor = AppTheme.Canvas
        };
        ApplyDoubleBuffering(root);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _mainTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(18, 8),
            Font = AppTheme.CaptionFont(9.5f),
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary
        };
        ApplyDoubleBuffering(_mainTabs);

        _mainTabs.SuspendLayout();
        _deferredTabBuilders["Profiles"] = BuildProfilesTab;
        _deferredTabBuilders["Tools"] = BuildToolsTab;
        _deferredTabBuilders["Resolution"] = BuildResolutionTab;
        _deferredTabBuilders["Frame Gen Lab"] = BuildFrameGenLabTab;
        _mainTabs.TabPages.Add(BuildSafeTab("Dashboard", BuildDashboardTab));
        _mainTabs.TabPages.Add(CreateDeferredTab("Profiles"));
        _mainTabs.TabPages.Add(CreateDeferredTab("Tools"));
        _mainTabs.TabPages.Add(CreateDeferredTab("Resolution"));
        _mainTabs.TabPages.Add(CreateDeferredTab("Frame Gen Lab"));
        _mainTabs.ResumeLayout();
        _mainTabs.SelectedIndexChanged += (_, _) => HandleMainTabChanged();

        var experienceCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 16,
            EnableSpotlight = false,
            Margin = new Padding(10, 0, 10, 0),
            Padding = new Padding(16, 12, 16, 12),
            Height = 76
        };

        var experienceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        experienceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        experienceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        experienceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        experienceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        experienceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _experienceStatusTitleLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(11.5f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = Color.Transparent,
            Text = "CloudFrame ready"
        };
        _experienceStatusDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
            Text = "CloudFrame is standing by for a profile, live session, or quick action."
        };
        _experienceStatusContextLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.3f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(18, 2, 18, 0),
            Text = "Selected profile: none"
        };
        _experienceVersionLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.2f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Text = $"Version {Application.ProductVersion}"
        };

        experienceLayout.Controls.Add(_experienceStatusTitleLabel, 0, 0);
        experienceLayout.Controls.Add(_experienceStatusDetailLabel, 0, 1);
        experienceLayout.Controls.Add(_experienceStatusContextLabel, 1, 0);
        experienceLayout.SetRowSpan(_experienceStatusContextLabel, 2);
        experienceLayout.Controls.Add(_experienceVersionLabel, 2, 0);
        experienceLayout.SetRowSpan(_experienceVersionLabel, 2);
        experienceCard.Controls.Add(experienceLayout);

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

        root.Controls.Add(_mainTabs, 0, 0);
        root.Controls.Add(experienceCard, 0, 1);
        root.Controls.Add(_footerLabel, 0, 2);
        Controls.Add(root);
    }

    private TabPage BuildSafeTab(string title, Func<TabPage> builder)
    {
        try
        {
            _logger.Log($"Building {title} tab.");
            var tab = builder();
            PrepareControlTreeForFastPaint(tab);
            return tab;
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

    private TabPage CreateDeferredTab(string title)
    {
        var tab = new TabPage(title)
        {
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary,
            Tag = "Deferred"
        };

        tab.Controls.Add(new Label
        {
            Text = $"{title} will load when opened.",
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(24, 24)
        });

        return tab;
    }

    private void HandleMainTabChanged()
    {
        if (_mainTabs is null || _tabSwitchInProgress || IsDisposed || Disposing)
        {
            return;
        }

        try
        {
            _tabSwitchInProgress = true;
            _mainTabs.SuspendLayout();
            EnsureSelectedTabBuilt();
        }
        finally
        {
            _mainTabs.ResumeLayout(true);
            _tabSwitchInProgress = false;
        }

        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            UpdateAnimatedUiState();
            RefreshVisibleTabIfDirty();
            _mainTabs.Invalidate(true);
            _mainTabs.SelectedTab?.Invalidate(true);
            _mainTabs.SelectedTab?.Refresh();
        }));
    }

    private void EnsureSelectedTabBuilt()
    {
        if (_mainTabs?.SelectedTab is not TabPage selectedTab)
        {
            return;
        }

        if (!Equals(selectedTab.Tag, "Deferred"))
        {
            return;
        }

        var title = selectedTab.Text;
        if (!_deferredTabBuilders.TryGetValue(title, out var builder))
        {
            return;
        }

        var selectedIndex = _mainTabs.SelectedIndex;
        var builtTab = BuildSafeTab(title, builder);
        builtTab.Tag = null;
        _mainTabs.TabPages.RemoveAt(selectedIndex);
        _mainTabs.TabPages.Insert(selectedIndex, builtTab);
        _mainTabs.SelectedIndex = selectedIndex;
    }

    private void RefreshVisibleTabIfDirty()
    {
        if (IsDashboardTabActive() && _dashboardUiDirty)
        {
            RefreshDashboardUiCore();
        }

        if (IsProfilesTabActive() && _profileStudioDirty)
        {
            RefreshProfileStudioCardCore();
        }

        if (IsToolsTabActive() && _toolsUiDirty)
        {
            RefreshToolsUiCore();
        }
    }

    private void QueueDeferredTabWarmup()
    {
        if (_deferredWarmupStarted || _mainTabs is null || IsDisposed || Disposing)
        {
            return;
        }

        _deferredWarmupStarted = true;
        _deferredTabWarmupQueue.Clear();

        foreach (TabPage tab in _mainTabs.TabPages)
        {
            if (Equals(tab.Tag, "Deferred"))
            {
                _deferredTabWarmupQueue.Enqueue(tab.Text);
            }
        }

        if (_deferredTabWarmupQueue.Count > 0)
        {
            _deferredTabWarmupTimer.Start();
        }
    }

    private void WarmNextDeferredTab()
    {
        if (IsDisposed || Disposing || _mainTabs is null)
        {
            _deferredTabWarmupTimer.Stop();
            return;
        }

        if (_tabSwitchInProgress)
        {
            return;
        }

        while (_deferredTabWarmupQueue.Count > 0)
        {
            var title = _deferredTabWarmupQueue.Dequeue();
            var tabIndex = FindDeferredTabIndex(title);
            if (tabIndex < 0)
            {
                continue;
            }

            _mainTabs.SuspendLayout();
            try
            {
                BuildDeferredTabAt(tabIndex);
            }
            finally
            {
                _mainTabs.ResumeLayout(true);
            }

            return;
        }

        _deferredTabWarmupTimer.Stop();
    }

    private int FindDeferredTabIndex(string title)
    {
        if (_mainTabs is null)
        {
            return -1;
        }

        for (var i = 0; i < _mainTabs.TabPages.Count; i++)
        {
            var tab = _mainTabs.TabPages[i];
            if (Equals(tab.Tag, "Deferred") && string.Equals(tab.Text, title, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void BuildDeferredTabAt(int tabIndex)
    {
        if (_mainTabs is null || tabIndex < 0 || tabIndex >= _mainTabs.TabPages.Count)
        {
            return;
        }

        var tab = _mainTabs.TabPages[tabIndex];
        if (!Equals(tab.Tag, "Deferred"))
        {
            return;
        }

        var title = tab.Text;
        if (!_deferredTabBuilders.TryGetValue(title, out var builder))
        {
            return;
        }

        var selectedIndex = _mainTabs.SelectedIndex;
        var builtTab = BuildSafeTab(title, builder);
        builtTab.Tag = null;
        _mainTabs.TabPages.RemoveAt(tabIndex);
        _mainTabs.TabPages.Insert(tabIndex, builtTab);

        if (selectedIndex >= 0 && selectedIndex < _mainTabs.TabPages.Count)
        {
            _mainTabs.SelectedIndex = selectedIndex;
        }
    }

    private static void PrepareControlTreeForFastPaint(Control root)
    {
        ApplyDoubleBuffering(root);
        foreach (Control child in root.Controls)
        {
            PrepareControlTreeForFastPaint(child);
        }
    }

    private static void ApplyDoubleBuffering(Control control)
    {
        try
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(control, true, null);
        }
        catch
        {
        }
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
        var scrollHost = new DashboardBackdropPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        _dashboardBackdropPanel = scrollHost;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 13,
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
        var heroBoostButton = AppTheme.CreateButton("Optimize Automatically", primary: true, width: 208);
        heroBoostButton.Click += async (_, _) => await UniversalBoostNowAsync();
        var heroStartButton = AppTheme.CreateButton("Scan Now", width: 164);
        heroStartButton.Click += async (_, _) => await ManualScanNowAsync();
        _advancedViewCheckBox = new CheckBox
        {
            Text = "Advanced View",
            AutoSize = true,
            Checked = false,
            ForeColor = Color.White,
            Font = AppTheme.CaptionFont(9.5f),
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0)
        };
        _advancedViewCheckBox.CheckedChanged += (_, _) => UpdateDashboardAdvancedMode();
        heroButtons.Controls.Add(heroBoostButton);
        heroButtons.Controls.Add(heroStartButton);
        heroButtons.Controls.Add(_advancedViewCheckBox);

        var heroMascot = CreateHeroMascot();

        heroLayout.Controls.Add(heroText, 0, 0);
        if (heroMascot is not null)
        {
            heroLayout.Controls.Add(heroMascot, 1, 0);
        }

        heroLayout.Controls.Add(heroButtons, 2, 0);
        hero.Controls.Add(heroLayout);

        var summaryStrip = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 12, 0, 10),
            InnerPadding = new Padding(16)
        };
        var summaryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        _dashboardSummaryLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        };
        _dashboardSummaryDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 6, 0, 0)
        };
        summaryLayout.Controls.Add(_dashboardSummaryLabel, 0, 0);
        summaryLayout.Controls.Add(_dashboardSummaryDetailLabel, 0, 1);
        summaryStrip.Controls.Add(summaryLayout);

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

        var metricCardRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 8),
            BackColor = AppTheme.Canvas
        };
        var gaugeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = AppTheme.Canvas
        };
        _boostStatusCard = new MetricCard();
        _compatibilityCard = new MetricCard();
        _overlayCard = new MetricCard();
        _impactCard = new MetricCard();
        _deltaCard = new MetricCard();
        metricCardRow.Controls.Add(_boostStatusCard);
        metricCardRow.Controls.Add(_compatibilityCard);
        metricCardRow.Controls.Add(_overlayCard);
        metricCardRow.Controls.Add(_impactCard);
        metricCardRow.Controls.Add(_deltaCard);
        gaugeRow.Controls.Add(_cpuRing);
        gaugeRow.Controls.Add(_gpuRing);
        gaugeRow.Controls.Add(_fpsRing);
        _uiToolTip.SetToolTip(_boostStatusCard, "Current boost state for the selected game or universal session.");
        _uiToolTip.SetToolTip(_compatibilityCard, "Whether CloudFrame is using a safer compatibility path because of anti-cheat or engine behavior.");
        _uiToolTip.SetToolTip(_overlayCard, "Current overlay state and the last readable FPS/CPU/GPU snapshot.");
        _uiToolTip.SetToolTip(_impactCard, "Boost plan scope is CloudFrame's estimate of how much tuning is active.");
        _uiToolTip.SetToolTip(_deltaCard, "Measured delta is the real observed before/after FPS comparison when enough samples exist.");
        _uiToolTip.SetToolTip(_cpuRing, "CPU utilization across the machine right now.");
        _uiToolTip.SetToolTip(_gpuRing, "GPU utilization from Windows GPU telemetry.");
        _uiToolTip.SetToolTip(_fpsRing, "Live FPS sampled from the current boosted game session.");

        var gaugePanel = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 128,
            MinimumSize = new Size(0, 128),
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
        _uiToolTip.SetToolTip(_boostGauge, "Boost scope shows planned or applied optimization actions, not guaranteed FPS gain.");
        _boostGauge.Caption = "Boost gauge";
        _boostGauge.Detail = "Preset-driven estimate of tuning strength and scope.";
        gaugePanel.Controls.Add(_boostGauge);
        _dashboardGaugePanel = gaugePanel;

        var performanceLabPanel = new CardPanel
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
        var performanceLabLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 6,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performanceLabLayout.Controls.Add(new Label
        {
            Text = "Performance Lab",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        }, 0, 0);
        _performanceLabSummaryLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.AccentSoft,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 8),
            Text = "CloudFrame is calibrating benchmark-grade session metrics."
        };
        _performanceLabBaselineLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Text = "Baseline metrics appear after CloudFrame sees a real profiled game idling for a bit."
        };
        _performanceLabLiveLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 4, 0, 0),
            Text = "Live metrics appear during an active boosted session."
        };
        _performanceLabPacingLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 4, 0, 0),
            Text = "Pacing score reflects frametime consistency, not just headline FPS."
        };
        var copyPerformanceLabButton = AppTheme.CreateButton("Copy Metrics Snapshot", width: 190);
        copyPerformanceLabButton.Margin = new Padding(0, 10, 0, 0);
        copyPerformanceLabButton.Click += (_, _) => CopyPerformanceLabSnapshot();
        performanceLabLayout.Controls.Add(_performanceLabSummaryLabel, 0, 1);
        performanceLabLayout.Controls.Add(_performanceLabBaselineLabel, 0, 2);
        performanceLabLayout.Controls.Add(_performanceLabLiveLabel, 0, 3);
        performanceLabLayout.Controls.Add(_performanceLabPacingLabel, 0, 4);
        performanceLabLayout.Controls.Add(copyPerformanceLabButton, 0, 5);
        performanceLabPanel.Controls.Add(performanceLabLayout);
        _dashboardPerformanceLabPanel = performanceLabPanel;

        var sessionReportPanel = new CardPanel
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
        var sessionReportLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        sessionReportLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sessionReportLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sessionReportLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sessionReportLayout.Controls.Add(new Label
        {
            Text = "Last boost result",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        }, 0, 0);
        _sessionReportLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.AccentSoft,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 6),
            Text = "No completed boost session yet."
        };
        _sessionReportDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Text = "Boost a selected game, play for a bit, then restore to see a measured result summary here."
        };
        sessionReportLayout.Controls.Add(_sessionReportLabel, 0, 1);
        sessionReportLayout.Controls.Add(_sessionReportDetailLabel, 0, 2);
        sessionReportPanel.Controls.Add(sessionReportLayout);
        _dashboardSessionReportPanel = sessionReportPanel;

        var recommendationPanel = new CardPanel
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
        var recommendationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        recommendationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recommendationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recommendationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recommendationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recommendationLayout.Controls.Add(new Label
        {
            Text = "Recommended profile tuning",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        }, 0, 0);
        _recommendationLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.AccentSoft,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 6),
            Text = "Need a live session"
        };
        _recommendationDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Text = "Boost a game and let CloudFrame watch the session for a few seconds to unlock tuning recommendations."
        };
        var applyRecommendationButton = AppTheme.CreateButton("Apply Recommendation", width: 178);
        applyRecommendationButton.Click += (_, _) => ApplyCurrentRecommendation();
        recommendationLayout.Controls.Add(_recommendationLabel, 0, 1);
        recommendationLayout.Controls.Add(_recommendationDetailLabel, 0, 2);
        recommendationLayout.Controls.Add(applyRecommendationButton, 0, 3);
        recommendationPanel.Controls.Add(recommendationLayout);
        _dashboardRecommendationPanel = recommendationPanel;

        var advisorPanel = new CardPanel
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
        var advisorLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        advisorLayout.Controls.Add(new Label
        {
            Text = "AI Performance Advisor",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        }, 0, 0);
        _advisorLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.AccentSoft,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 8, 0, 6)
        };
        _advisorDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface
        };
        advisorLayout.Controls.Add(_advisorLabel, 0, 1);
        advisorLayout.Controls.Add(_advisorDetailLabel, 0, 2);
        advisorPanel.Controls.Add(advisorLayout);

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
            FlowDirection = FlowDirection.LeftToRight,
            Dock          = DockStyle.Top,
            Margin        = new Padding(0),
            Padding       = new Padding(0, 0, 0, 6),
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

        var startButton = AppTheme.CreateButton("Scan Now", width: 164);
        startButton.Click += async (_, _) => await ManualScanNowAsync();

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
        _detectedFilterTextBox.PlaceholderText = "Filter scanned processes by profile, process, PID, path, or status";
        _detectedFilterTextBox.Margin = new Padding(0, 0, 0, 10);
        _detectedFilterTextBox.TextChanged += (_, _) => RefreshDetectedGrid();

        _dashboardScanSummaryLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0, 0, 0, 8),
            Text = "Press Scan Now to inspect live running processes and pick one for a quick on-the-fly boost."
        };

        _detectedGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MinimumSize = new Size(0, 180)
        };
        AppTheme.StyleGrid(_detectedGrid);
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Profile", Width = 180 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "Process", Width = 140 });
        _detectedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", Width = 96 });
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
            RowCount = 3,
            ColumnCount = 1,
            MinimumSize = new Size(0, 248)
        };
        detectedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detectedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detectedPanel.Controls.Add(_dashboardScanSummaryLabel, 0, 0);
        detectedPanel.Controls.Add(_detectedFilterTextBox, 0, 1);
        detectedPanel.Controls.Add(_detectedGrid, 0, 2);

        var matchesPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 12),
            InnerPadding = new Padding(16),
            Height = 340,
            MinimumSize = new Size(0, 340)
        };
        var matchesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.Surface
        };
        matchesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        matchesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        matchesLayout.Controls.Add(new Label
        {
            Text = "Running Processes",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);
        matchesLayout.Controls.Add(detectedPanel, 0, 1);
        matchesPanel.Controls.Add(matchesLayout);
        _dashboardMatchesGroup = matchesPanel;

        var logPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 0, 12),
            InnerPadding = new Padding(16),
            Height = 220,
            MinimumSize = new Size(0, 220)
        };
        var logLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.Surface
        };
        logLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logLayout.Controls.Add(new Label
        {
            Text = "Activity Log",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);
        logLayout.Controls.Add(_logTextBox, 0, 1);
        logPanel.Controls.Add(logLayout);
        _dashboardLogGroup = logPanel;

        root.Controls.Add(hero, 0, 0);
        root.Controls.Add(summaryStrip, 0, 1);
        root.Controls.Add(metricCardRow, 0, 2);
        root.Controls.Add(gaugeRow, 0, 3);
        root.Controls.Add(advisorPanel, 0, 4);
        root.Controls.Add(controlPanel, 0, 5);
        root.Controls.Add(statusPanel, 0, 6);
        root.Controls.Add(_dashboardMatchesGroup, 0, 7);
        root.Controls.Add(gaugePanel, 0, 8);
        root.Controls.Add(performanceLabPanel, 0, 9);
        root.Controls.Add(sessionReportPanel, 0, 10);
        root.Controls.Add(recommendationPanel, 0, 11);
        root.Controls.Add(_dashboardLogGroup, 0, 12);

        void syncDashboardLayout()
        {
            var availableWidth = Math.Max(760, scrollHost.ClientSize.Width - 8);
            root.MaximumSize = new Size(availableWidth, 0);
            root.Width = availableWidth;
            metricCardRow.MaximumSize = new Size(availableWidth, 0);
            gaugeRow.MaximumSize = new Size(availableWidth, 0);
            infoFlow.MaximumSize = new Size(availableWidth, 0);
            buttonFlow.MaximumSize = new Size(availableWidth, 0);
            controlToggleFlow.MaximumSize = new Size(Math.Max(380, availableWidth - 56), 0);

            var heroTextBudget = availableWidth - heroButtons.Width - (heroMascot?.Width ?? 0) - 140;
            heroSummaryLabel.MaximumSize = new Size(Math.Max(320, heroTextBudget), 0);
        }

        scrollHost.Resize += (_, _) => syncDashboardLayout();
        syncDashboardLayout();
        UpdateDashboardAdvancedMode();

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
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(12, 12, 12, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
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

        var rightScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(0)
        };

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Canvas
        };
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            Margin        = new Padding(0),
            AutoSizeMode  = AutoSizeMode.GrowAndShrink
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
            Margin   = new Padding(0, 14, 0, 0),
            Dock     = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        _profileSnapshotCard.EditRequested += (_, _) => EditSelectedProfile();
        _profileSnapshotCard.SyncRequested += async (_, _) => await ManualScanNowAsync();
        _profileSnapshotCard.SettingsRequested += (_, _) =>
        {
            if (_mainTabs.TabCount > 2)
            {
                _mainTabs.SelectedIndex = 2;
            }
        };
        _profileSnapshotCard.FeedbackRequested += (_, _) => ExportIssueReport();
        _profileSnapshotCard.ProfileChanged += (_, _) =>
        {
            var profile = SelectedProfile;
            SaveSettings();
            BindProfiles();
            SelectProfile(profile);
            SyncPresetCardSelection();
            UpdateStatusLabels();
            UpdateDashboardCards();
            RequestProfileStudioRefresh();
        };
        RequestProfileStudioRefresh();

        rightPanel.Controls.Add(presetSection, 0, 0);
        rightPanel.Controls.Add(_profileSnapshotCard, 0, 1);

        rightScrollHost.Controls.Add(rightPanel);
        layout.Controls.Add(leftPanel, 0, 0);
        layout.Controls.Add(rightScrollHost, 1, 0);

        void syncProfilesWorkspaceLayout()
        {
            var availableWidth = Math.Max(680, rightScrollHost.ClientSize.Width - 24);
            rightPanel.MaximumSize = new Size(availableWidth, 0);
            rightPanel.Width = availableWidth;
            presetSub.MaximumSize = new Size(Math.Max(360, availableWidth - 40), 0);
            cardRack.MaximumSize = new Size(Math.Max(360, availableWidth - 12), 0);
            _profileSnapshotCard.MaximumSize = new Size(availableWidth, 0);
            _profileSnapshotCard.Width = availableWidth;
        }

        rightScrollHost.Resize += (_, _) => syncProfilesWorkspaceLayout();
        syncProfilesWorkspaceLayout();

        tab.Controls.Add(layout);
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
            RowCount = 4,
            Padding = new Padding(16, 20, 16, 16),
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttonFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            WrapContents = true,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0, 4, 0, 0)
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

        var reportIssueButton = AppTheme.CreateButton("Export Issue Report", width: 166);
        reportIssueButton.Click += (_, _) => ExportIssueReport();

        buttonFlow.Controls.Add(refreshPlansButton);
        buttonFlow.Controls.Add(cleanShaderCacheButton);
        buttonFlow.Controls.Add(trimMemoryButton);
        buttonFlow.Controls.Add(closeBackgroundButton);
        buttonFlow.Controls.Add(rerunMaintenanceButton);
        buttonFlow.Controls.Add(openDataButton);
        buttonFlow.Controls.Add(openLogButton);
        buttonFlow.Controls.Add(clearLogButton);
        buttonFlow.Controls.Add(reportIssueButton);

        var safetyCard = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
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

        var actionsCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 20,
            EnableSpotlight = false,
            Margin = new Padding(0, 6, 0, 16),
            InnerPadding = new Padding(22, 24, 22, 20)
        };
        var actionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionsLayout.Controls.Add(new Label
        {
            Text = "Tools Control Center",
            AutoSize = true,
            Font = AppTheme.TitleFont(16f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        actionsLayout.Controls.Add(new Label
        {
            Text = "Everything here is organized for fast setup, safe tuning, and easy previews. Start with a preset if you're new, then refine only the modules you care about.",
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            Font = AppTheme.BodyFont(9.6f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);
        actionsLayout.Controls.Add(buttonFlow, 0, 2);
        actionsCard.Controls.Add(actionsLayout);

        var wizardCard = BuildQuickSetupWizardCard();
        wizardCard.Dock = DockStyle.Fill;
        var previewCard = BuildToolsPreviewCard();
        previewCard.Dock = DockStyle.Fill;
        var themeCard = BuildThemeSettingsCard();
        var overlayCard = BuildOverlaySettingsCard();
        var defaultsCard = BuildSessionDefaultsCard();
        var readinessCard = BuildReadinessCard();
        var tweaksCard = BuildBoostTweaksCard();

        var powerPlansCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };
        var powerPlansLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        powerPlansLayout.Controls.Add(new Label
        {
            Text = "Power Plan Browser",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        powerPlansLayout.Controls.Add(new Label
        {
            Text = "This is the live Windows power-plan list CloudFrame can switch between for universal boosts and saved profiles.",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);
        powerPlansLayout.Controls.Add(_powerPlanList, 0, 2);
        powerPlansCard.Controls.Add(powerPlansLayout);

        var toolStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Canvas
        };
        toolStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        toolStack.Controls.Add(wizardCard, 0, 0);
        toolStack.Controls.Add(previewCard, 0, 1);
        toolStack.Controls.Add(themeCard, 0, 2);
        toolStack.Controls.Add(overlayCard, 0, 3);
        toolStack.Controls.Add(defaultsCard, 0, 4);
        toolStack.Controls.Add(tweaksCard, 0, 5);
        toolStack.Controls.Add(readinessCard, 0, 6);
        toolStack.Controls.Add(safetyCard, 0, 7);
        toolStack.Controls.Add(powerPlansCard, 0, 8);

        root.Controls.Add(actionsCard, 0, 0);
        root.Controls.Add(toolStack, 0, 1);

        void syncToolsLayout()
        {
            var availableWidth = Math.Max(860, scrollHost.ClientSize.Width - 8);
            var cardWidth = Math.Max(760, availableWidth - 8);
            var actionWrapWidth = Math.Max(720, cardWidth - 44);

            root.MaximumSize = new Size(availableWidth, 0);
            root.Width = availableWidth;
            toolStack.MaximumSize = new Size(availableWidth, 0);
            toolStack.Width = availableWidth;
            actionsCard.MaximumSize = new Size(cardWidth, 0);
            actionsCard.Width = cardWidth;
            buttonFlow.MaximumSize = new Size(actionWrapWidth, 0);

            wizardCard.MaximumSize = new Size(cardWidth, 0);
            wizardCard.Width = cardWidth;
            previewCard.MaximumSize = new Size(cardWidth, 0);
            previewCard.Width = cardWidth;
            themeCard.MaximumSize = new Size(cardWidth, 0);
            themeCard.Width = cardWidth;
            overlayCard.MaximumSize = new Size(cardWidth, 0);
            overlayCard.Width = cardWidth;
            defaultsCard.MaximumSize = new Size(cardWidth, 0);
            defaultsCard.Width = cardWidth;
            tweaksCard.MaximumSize = new Size(cardWidth, 0);
            tweaksCard.Width = cardWidth;
            readinessCard.MaximumSize = new Size(cardWidth, 0);
            readinessCard.Width = cardWidth;
            safetyCard.MaximumSize = new Size(cardWidth, 0);
            safetyCard.Width = cardWidth;
            powerPlansCard.MaximumSize = new Size(cardWidth, 0);
            powerPlansCard.Width = cardWidth;
        }

        scrollHost.Resize += (_, _) => syncToolsLayout();
        syncToolsLayout();
        BindLoadedPowerPlans();
        UpdateShaderCacheLabel();

        scrollHost.Controls.Add(root);
        tab.Controls.Add(scrollHost);
        RequestToolsUiRefresh();
        return tab;
    }

    private TabPage BuildResolutionTab()
    {
        var tab = new TabPage("Resolution")
        {
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary
        };

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
            Padding = new Padding(16, 20, 16, 16),
            BackColor = AppTheme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var headerCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 20,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 16),
            InnerPadding = new Padding(22, 22, 22, 18)
        };
        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        headerLayout.Controls.Add(new Label
        {
            Text = "Resolution Control",
            AutoSize = true,
            Font = AppTheme.TitleFont(16f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        headerLayout.Controls.Add(new Label
        {
            Text = "Pick a running game window, choose a target resolution, optionally apply borderless, and let CloudFrame resize the live window without leaving the app.",
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            Font = AppTheme.BodyFont(9.6f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 14)
        }, 0, 1);

        var actionFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };
        var refreshProcessesButton = AppTheme.CreateButton("Refresh Running Windows", primary: true, width: 188);
        refreshProcessesButton.Click += async (_, _) => await RefreshResolutionProcessesAsync();
        var applyResolutionButton = AppTheme.CreateButton("Apply Resolution", width: 152);
        applyResolutionButton.Click += async (_, _) => await ApplyResolutionLayoutAsync();
        var restoreBorderButton = AppTheme.CreateButton("Restore Border", width: 132);
        restoreBorderButton.Click += (_, _) => RestoreSelectedWindowBorder();
        var useWindowSizeButton = AppTheme.CreateButton("Read Current Size", width: 150);
        useWindowSizeButton.Click += (_, _) => SyncResolutionSizeFromSelection();

        actionFlow.Controls.Add(refreshProcessesButton);
        actionFlow.Controls.Add(applyResolutionButton);
        actionFlow.Controls.Add(restoreBorderButton);
        actionFlow.Controls.Add(useWindowSizeButton);
        headerLayout.Controls.Add(actionFlow, 0, 2);
        headerCard.Controls.Add(headerLayout);

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var processCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 12, 0),
            InnerPadding = new Padding(18)
        };
        var processLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        processLayout.Controls.Add(new Label
        {
            Text = "Target Window",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        processLayout.Controls.Add(new Label
        {
            Text = "CloudFrame lists visible top-level windows only, so you can target the real game window instead of a hidden launcher helper.",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 14)
        }, 0, 1);

        processLayout.Controls.Add(new Label
        {
            Text = "Process",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 2);
        _resolutionProcessComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 420,
            Margin = new Padding(0, 0, 0, 12)
        });
        _resolutionProcessComboBox.SelectedIndexChanged += (_, _) => RefreshResolutionWindowChoices();
        processLayout.Controls.Add(_resolutionProcessComboBox, 0, 3);

        processLayout.Controls.Add(new Label
        {
            Text = "Window",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 4);
        _resolutionWindowComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 420
        });
        _resolutionWindowComboBox.SelectedIndexChanged += (_, _) => SyncResolutionSizeFromSelection();
        processLayout.Controls.Add(_resolutionWindowComboBox, 0, 5);
        processCard.Controls.Add(processLayout);

        var controlsCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(12, 0, 0, 0),
            InnerPadding = new Padding(18)
        };
        var controlsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        controlsLayout.Controls.Add(new Label
        {
            Text = "Window Layout",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        controlsLayout.SetColumnSpan(controlsLayout.Controls[0], 2);
        controlsLayout.Controls.Add(new Label
        {
            Text = "Use a preset or enter a custom width and height. Borderless removes the standard window chrome and reapplies the frame cleanly.",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 1);
        controlsLayout.SetColumnSpan(controlsLayout.Controls[1], 2);

        controlsLayout.Controls.Add(new Label
        {
            Text = "Preset",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 12, 8)
        }, 0, 2);
        _resolutionPresetComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(0, 0, 0, 10)
        });
        _resolutionPresetComboBox.Items.AddRange(
        [
            "1280 x 720",
            "1600 x 900",
            "1920 x 1080",
            "2560 x 1440",
            "3440 x 1440",
            "3840 x 2160",
            "Custom"
        ]);
        _resolutionPresetComboBox.SelectedIndexChanged += (_, _) => ApplyResolutionPresetSelection();
        _resolutionPresetComboBox.SelectedIndex = 2;
        controlsLayout.Controls.Add(_resolutionPresetComboBox, 1, 2);

        controlsLayout.Controls.Add(new Label
        {
            Text = "Width",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 12, 8)
        }, 0, 3);
        _resolutionWidthInput = new NumericUpDown
        {
            Width = 140,
            Minimum = 320,
            Maximum = 7680,
            Increment = 10,
            Value = 1920,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppTheme.BodyFont()
        };
        controlsLayout.Controls.Add(_resolutionWidthInput, 1, 3);

        controlsLayout.Controls.Add(new Label
        {
            Text = "Height",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 12, 8)
        }, 0, 4);
        _resolutionHeightInput = new NumericUpDown
        {
            Width = 140,
            Minimum = 200,
            Maximum = 4320,
            Increment = 10,
            Value = 1080,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppTheme.BodyFont(),
            Margin = new Padding(0, 10, 0, 0)
        };
        controlsLayout.Controls.Add(_resolutionHeightInput, 1, 4);

        _resolutionBorderlessCheckBox = new CheckBox
        {
            Text = "Apply borderless window mode",
            AutoSize = true,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Font = AppTheme.BodyFont(9.8f),
            Margin = new Padding(0, 14, 0, 0)
        };
        controlsLayout.Controls.Add(_resolutionBorderlessCheckBox, 0, 5);
        controlsLayout.SetColumnSpan(_resolutionBorderlessCheckBox, 2);

        _resolutionStatusLabel = new Label
        {
            Text = "Open the tab and refresh running windows when you want to target a live game.",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 14, 0, 0)
        };
        controlsLayout.Controls.Add(_resolutionStatusLabel, 0, 6);
        controlsLayout.SetColumnSpan(_resolutionStatusLabel, 2);
        controlsCard.Controls.Add(controlsLayout);

        workspace.Controls.Add(processCard, 0, 0);
        workspace.Controls.Add(controlsCard, 1, 0);

        root.Controls.Add(headerCard, 0, 0);
        root.Controls.Add(workspace, 0, 1);

        void syncResolutionLayout()
        {
            var availableWidth = Math.Max(920, scrollHost.ClientSize.Width - 8);
            var cardWidth = Math.Max(420, (availableWidth - 24) / 2);
            root.MaximumSize = new Size(availableWidth, 0);
            root.Width = availableWidth;
            headerCard.MaximumSize = new Size(availableWidth, 0);
            headerCard.Width = availableWidth;
            processCard.MaximumSize = new Size(cardWidth, 0);
            processCard.Width = cardWidth;
            controlsCard.MaximumSize = new Size(cardWidth, 0);
            controlsCard.Width = cardWidth;
        }

        scrollHost.Resize += (_, _) => syncResolutionLayout();
        syncResolutionLayout();

        scrollHost.Controls.Add(root);
        tab.Controls.Add(scrollHost);

        BeginInvoke(new Action(async () => await RefreshResolutionProcessesAsync()));
        return tab;
    }

    private TabPage BuildFrameGenLabTab()
    {
        var techReport = _gpuTechnologyAdvisorService.GetReport();

        var tab = new TabPage("Frame Gen Lab")
        {
            BackColor = AppTheme.Canvas,
            ForeColor = AppTheme.TextPrimary
        };

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
            Padding = new Padding(16, 20, 16, 16),
            BackColor = AppTheme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var heroCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 20,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 16),
            InnerPadding = new Padding(22, 22, 22, 18)
        };
        var heroLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = AppTheme.Surface
        };
        heroLayout.Controls.Add(new Label
        {
            Text = "Experimental Frame Gen Lab",
            AutoSize = true,
            Font = AppTheme.TitleFont(16f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        heroLayout.Controls.Add(new Label
        {
            Text = "This lab is CloudFrame's future-facing space for external frame interpolation ideas. It is intentionally gated: anti-cheat titles stay off-limits, the safest capture path stays external, and nothing here claims Lossless Scaling parity yet.",
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.BodyFont(9.6f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 8, 0, 14)
        }, 0, 1);

        _frameGenEnableCheckBox = new CheckBox
        {
            Text = "Enable experimental frame generation lab features",
            AutoSize = true,
            Checked = _settings.EnableExperimentalFrameGen,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Font = AppTheme.BodyFont(10f),
            Margin = new Padding(0, 0, 0, 8)
        };
        _frameGenEnableCheckBox.CheckedChanged += (_, _) =>
        {
            _settings.EnableExperimentalFrameGen = _frameGenEnableCheckBox.Checked;
            SaveSettings();
            UpdateFrameGenLabStatus();
        };
        heroLayout.Controls.Add(_frameGenEnableCheckBox, 0, 2);

        _frameGenStatusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.BodyFont(9.4f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 0)
        };
        heroLayout.Controls.Add(_frameGenStatusLabel, 0, 3);
        _frameGenGpuSummaryLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.BodyFont(9.2f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 0, 0)
        };
        heroLayout.Controls.Add(_frameGenGpuSummaryLabel, 0, 4);
        heroCard.Controls.Add(heroLayout);

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Canvas,
            Margin = new Padding(0)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var pipelineCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 12, 0),
            InnerPadding = new Padding(18)
        };
        var pipelineLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        pipelineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pipelineLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddPipelineLabel(string text, int row, bool title = false, int span = 2, Padding? margin = null)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                Font = title ? AppTheme.TitleFont(13f) : AppTheme.BodyFont(9.5f),
                ForeColor = title ? AppTheme.TextPrimary : AppTheme.TextSecondary,
                Margin = margin ?? (title ? new Padding(0, 0, 0, 8) : new Padding(0, 0, 0, 12))
            };
            pipelineLayout.Controls.Add(label, 0, row);
            if (span > 1)
            {
                pipelineLayout.SetColumnSpan(label, span);
            }
        }

        AddPipelineLabel("Pipeline Settings", 0, title: true);
        AddPipelineLabel("These controls define the safest architecture CloudFrame will eventually use for external interpolation and presentation. They are configuration scaffolding for now, not a live frame-gen engine.", 1, margin: new Padding(0, 0, 0, 14));

        pipelineLayout.Controls.Add(new Label
        {
            Text = "Capture mode",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 12, 8)
        }, 0, 2);
        _frameGenCaptureComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            Margin = new Padding(0, 0, 0, 10)
        });
        foreach (var mode in Enum.GetValues<FrameGenCaptureMode>())
        {
            _frameGenCaptureComboBox.Items.Add(mode);
        }
        _frameGenCaptureComboBox.SelectedItem = _settings.FrameGenCaptureMode;
        _frameGenCaptureComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_frameGenCaptureComboBox.SelectedItem is FrameGenCaptureMode mode)
            {
                _settings.FrameGenCaptureMode = mode;
                SaveSettings();
                UpdateFrameGenLabStatus();
            }
        };
        pipelineLayout.Controls.Add(_frameGenCaptureComboBox, 1, 2);

        pipelineLayout.Controls.Add(new Label
        {
            Text = "Interpolation backend",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 12, 8)
        }, 0, 3);
        _frameGenBackendComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            Margin = new Padding(0, 0, 0, 10)
        });
        foreach (var backend in Enum.GetValues<FrameGenBackend>())
        {
            _frameGenBackendComboBox.Items.Add(backend);
        }
        _frameGenBackendComboBox.SelectedItem = _settings.FrameGenBackend;
        _frameGenBackendComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_frameGenBackendComboBox.SelectedItem is FrameGenBackend backend)
            {
                _settings.FrameGenBackend = backend;
                SaveSettings();
                UpdateFrameGenLabStatus();
            }
        };
        pipelineLayout.Controls.Add(_frameGenBackendComboBox, 1, 3);

        _frameGenRequireBorderlessCheckBox = new CheckBox
        {
            Text = "Require borderless/windowed presentation",
            AutoSize = true,
            Checked = _settings.FrameGenRequireBorderless,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Font = AppTheme.BodyFont(9.7f),
            Margin = new Padding(0, 8, 0, 0)
        };
        _frameGenRequireBorderlessCheckBox.CheckedChanged += (_, _) =>
        {
            _settings.FrameGenRequireBorderless = _frameGenRequireBorderlessCheckBox.Checked;
            SaveSettings();
            UpdateFrameGenLabStatus();
        };
        pipelineLayout.Controls.Add(_frameGenRequireBorderlessCheckBox, 0, 4);
        pipelineLayout.SetColumnSpan(_frameGenRequireBorderlessCheckBox, 2);

        _frameGenDisableOnAntiCheatCheckBox = new CheckBox
        {
            Text = "Disable automatically when anti-cheat is detected",
            AutoSize = true,
            Checked = _settings.FrameGenDisableOnAntiCheat,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Font = AppTheme.BodyFont(9.7f),
            Margin = new Padding(0, 8, 0, 0)
        };
        _frameGenDisableOnAntiCheatCheckBox.CheckedChanged += (_, _) =>
        {
            _settings.FrameGenDisableOnAntiCheat = _frameGenDisableOnAntiCheatCheckBox.Checked;
            SaveSettings();
            UpdateFrameGenLabStatus();
        };
        pipelineLayout.Controls.Add(_frameGenDisableOnAntiCheatCheckBox, 0, 5);
        pipelineLayout.SetColumnSpan(_frameGenDisableOnAntiCheatCheckBox, 2);

        _frameGenLowLatencyCheckBox = new CheckBox
        {
            Text = "Prioritize latency and pacing over maximum synthetic frame count",
            AutoSize = true,
            Checked = _settings.FrameGenPreferLowLatency,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Font = AppTheme.BodyFont(9.7f),
            Margin = new Padding(0, 8, 0, 0)
        };
        _frameGenLowLatencyCheckBox.CheckedChanged += (_, _) =>
        {
            _settings.FrameGenPreferLowLatency = _frameGenLowLatencyCheckBox.Checked;
            SaveSettings();
            UpdateFrameGenLabStatus();
        };
        pipelineLayout.Controls.Add(_frameGenLowLatencyCheckBox, 0, 6);
        pipelineLayout.SetColumnSpan(_frameGenLowLatencyCheckBox, 2);
        pipelineCard.Controls.Add(pipelineLayout);

        var researchCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(12, 0, 0, 0),
            InnerPadding = new Padding(18)
        };
        var researchLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        researchLayout.Controls.Add(new Label
        {
            Text = "Reality Check",
            AutoSize = true,
            Font = AppTheme.TitleFont(13f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        researchLayout.Controls.Add(new Label
        {
            Text = "NVIDIA Streamline / DLSS Frame Generation and AMD FSR 3 Frame Generation are game-side integrations, not something CloudFrame can ethically flip on from the outside. The realistic external path is capture + interpolation + presentation, with anti-cheat titles excluded by default.",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 1);
        researchLayout.Controls.Add(new Label
        {
            Text = "Candidate roadmap",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 2);
        researchLayout.Controls.Add(new Label
        {
            Text = "1. Capture borderless frames externally\n2. Build a low-risk interpolation prototype\n3. Add optional NVIDIA Optical Flow acceleration\n4. Gate the whole feature behind anti-cheat and compatibility checks\n5. Benchmark latency, pacing, and visual artifacts before shipping anything public",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 3);
        researchLayout.Controls.Add(new Label
        {
            Text = "Technology fit on this rig",
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 14, 0, 8)
        }, 0, 4);
        researchLayout.Controls.Add(CreateFrameGenCapabilityLabel(techReport.Dlss), 0, 5);
        researchLayout.Controls.Add(CreateFrameGenCapabilityLabel(techReport.Fsr), 0, 6);
        researchLayout.Controls.Add(CreateFrameGenCapabilityLabel(techReport.Xess), 0, 7);
        researchLayout.Controls.Add(CreateFrameGenCapabilityLabel(techReport.ExternalFrameGen), 0, 8);
        researchCard.Controls.Add(researchLayout);

        workspace.Controls.Add(pipelineCard, 0, 0);
        workspace.Controls.Add(researchCard, 1, 0);

        var prototypeCard = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 16, 0, 0),
            InnerPadding = new Padding(18)
        };
        var prototypeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        prototypeLayout.Controls.Add(new Label
        {
            Text = "Prototype Session Control",
            AutoSize = true,
            Font = AppTheme.TitleFont(13f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        prototypeLayout.Controls.Add(new Label
        {
            Text = "This is the first real step toward an external frame interpolation workflow: validate the selected game, block anti-cheat titles, and prep a clean borderless presentation path with minimal overhead.",
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);
        _frameGenEligibilityLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.TitleFont(12.5f),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 6)
        };
        prototypeLayout.Controls.Add(_frameGenEligibilityLabel, 0, 2);
        _frameGenEligibilityDetailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Font = AppTheme.BodyFont(9.4f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 12)
        };
        prototypeLayout.Controls.Add(_frameGenEligibilityDetailLabel, 0, 3);

        var prototypeActionFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };
        var evaluatePrototypeButton = AppTheme.CreateButton("Evaluate Selected Game", primary: true, width: 176);
        evaluatePrototypeButton.Click += (_, _) => UpdateFrameGenLabStatus();
        var preparePrototypeButton = AppTheme.CreateButton("Prepare Live Window", width: 164);
        preparePrototypeButton.Click += async (_, _) => await PrepareFrameGenPrototypeAsync();
        var launchPrototypeButton = AppTheme.CreateButton("Launch Prototype Session", width: 186);
        launchPrototypeButton.Click += async (_, _) => await LaunchFrameGenPrototypeAsync();
        prototypeActionFlow.Controls.Add(evaluatePrototypeButton);
        prototypeActionFlow.Controls.Add(preparePrototypeButton);
        prototypeActionFlow.Controls.Add(launchPrototypeButton);
        prototypeLayout.Controls.Add(prototypeActionFlow, 0, 4);
        prototypeCard.Controls.Add(prototypeLayout);

        root.Controls.Add(heroCard, 0, 0);
        root.Controls.Add(workspace, 0, 1);
        root.Controls.Add(prototypeCard, 0, 2);

        void syncFrameGenLayout()
        {
            var availableWidth = Math.Max(920, scrollHost.ClientSize.Width - 8);
            var cardWidth = Math.Max(420, (availableWidth - 24) / 2);
            root.MaximumSize = new Size(availableWidth, 0);
            root.Width = availableWidth;
            heroCard.MaximumSize = new Size(availableWidth, 0);
            heroCard.Width = availableWidth;
            pipelineCard.MaximumSize = new Size(cardWidth, 0);
            pipelineCard.Width = cardWidth;
            researchCard.MaximumSize = new Size(cardWidth, 0);
            researchCard.Width = cardWidth;
            prototypeCard.MaximumSize = new Size(availableWidth, 0);
            prototypeCard.Width = availableWidth;
        }

        scrollHost.Resize += (_, _) => syncFrameGenLayout();
        syncFrameGenLayout();
        _frameGenGpuSummaryLabel.Text = techReport.GpuNames.Count > 0
            ? $"Detected GPUs: {string.Join(" | ", techReport.GpuNames)}"
            : "Detected GPUs: CloudFrame could not identify the active GPU stack on this machine.";
        UpdateFrameGenLabStatus();

        scrollHost.Controls.Add(root);
        tab.Controls.Add(scrollHost);
        return tab;
    }

    private Control CreateFrameGenCapabilityLabel(TechCapability capability)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 0, 8),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        var title = new Label
        {
            Text = $"{capability.Name}: {capability.Status}",
            AutoSize = true,
            Font = AppTheme.BodyFont(9.6f),
            ForeColor = capability.Highlight ? AppTheme.AccentStrong : AppTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 2)
        };
        panel.Controls.Add(title);

        var detail = new Label
        {
            Text = capability.Detail,
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Font = AppTheme.BodyFont(9.1f),
            ForeColor = AppTheme.TextSecondary
        };
        panel.Controls.Add(detail);
        return panel;
    }

    private CardPanel BuildReadinessCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = AppTheme.Surface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Readiness Check",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);

        _diagnosticsSummaryLabel = new Label
        {
            Text = "CloudFrame will surface whether FPS capture, GPU telemetry, elevation, and overlay routing are ready on this machine.",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 6, 0, 12)
        };
        layout.Controls.Add(_diagnosticsSummaryLabel, 0, 1);

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0)
        };

        _presentMonReadyLabel = CreateDiagnosticLabel();
        _gpuReadyLabel = CreateDiagnosticLabel();
        _elevationReadyLabel = CreateDiagnosticLabel();
        _overlayReadyLabel = CreateDiagnosticLabel();
        _launcherHandoffLabel = CreateDiagnosticLabel();
        _trayModeLabel = CreateDiagnosticLabel();

        flow.Controls.Add(_presentMonReadyLabel);
        flow.Controls.Add(_gpuReadyLabel);
        flow.Controls.Add(_elevationReadyLabel);
        flow.Controls.Add(_overlayReadyLabel);
        flow.Controls.Add(_launcherHandoffLabel);
        flow.Controls.Add(_trayModeLabel);

        layout.Controls.Add(flow, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private CardPanel BuildQuickSetupWizardCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 20,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 12, 14),
            InnerPadding = new Padding(18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };

        layout.Controls.Add(new Label
        {
            Text = "Quick Setup Wizard",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "New here? Pick a starter mode and CloudFrame will configure the most important tools in under a minute.",
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        var presetFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 0, 10)
        };

        Button MakePresetButton(string title, string tip, Action apply, bool primary = false)
        {
            var button = AppTheme.CreateButton(title, primary: primary, width: 138);
            var tooltip = new ToolTip();
            tooltip.SetToolTip(button, tip);
            button.Click += (_, _) =>
            {
                apply();
                SaveSettings();
                ApplyMonitoringMode();
                SyncBoostControlState();
                SyncOverlayStudioControls();
                UpdateDashboardCards();
                RequestToolsUiRefresh();
                _logger.Log($"Applied Tools preset '{title}'.");
            };
            return button;
        }

        presetFlow.Controls.Add(MakePresetButton(
            "Balanced",
            "A safe default for most gamers: polished visuals, regular update checks, balanced monitoring, and recurring maintenance.",
            () => ApplyToolsPreset(AppThemePreset.Graphite, OverlayProfilePreset.Competitive, MonitoringMode.Balanced, true, true, true, true),
            primary: true));
        presetFlow.Controls.Add(MakePresetButton(
            "Performance",
            "Leans harder into gaming mode: stronger monitoring, maintenance, background memory priority, and tray-ready behavior.",
            () => ApplyToolsPreset(AppThemePreset.Midnight, OverlayProfilePreset.FullStats, MonitoringMode.Detailed, true, true, true, true)));
        presetFlow.Controls.Add(MakePresetButton(
            "Minimalist",
            "Keeps the shell lighter and simpler: minimal overlay, low-overhead monitoring, and fewer always-on helpers.",
            () => ApplyToolsPreset(AppThemePreset.Frost, OverlayProfilePreset.Minimal, MonitoringMode.Minimal, true, false, false, false)));

        var helperRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };
        var starterButton = AppTheme.CreateButton("Run First-Run Guide", width: 164);
        starterButton.Click += (_, _) => MaybeRunFirstRunSetup();
        var helpButton = AppTheme.CreateButton("?", width: 42);
        helpButton.Click += (_, _) => ShowToolsHelp(
            "Quick Setup Wizard",
            "Balanced keeps things safe and polished, Performance turns on more live visibility and maintenance, and Minimalist trims visual and monitoring overhead for users who just want the essentials.");
        helperRow.Controls.Add(starterButton);
        helperRow.Controls.Add(helpButton);

        layout.Controls.Add(presetFlow, 0, 2);
        layout.Controls.Add(helperRow, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private CardPanel BuildToolsPreviewCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 20,
            EnableSpotlight = false,
            Margin = new Padding(12, 0, 0, 14),
            InnerPadding = new Padding(18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };

        layout.Controls.Add(new Label
        {
            Text = "Live Preview Pane",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "This gives beginners a quick read on how the current Tools configuration will feel before they dive into every module.",
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        var previewSurface = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.SurfaceAlt,
            BorderColor = AppTheme.AccentStrong,
            CornerRadius = 18,
            EnableSpotlight = false,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 10)
        };

        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.SurfaceAlt
        };
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label CreatePreviewValue()
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(320, 0),
                Font = AppTheme.BodyFont(9.8f),
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.SurfaceAlt,
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        void AddRow(string label, Label value, int row)
        {
            previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewLayout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Font = AppTheme.CaptionFont(9.2f),
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.SurfaceAlt,
                Margin = new Padding(0, 0, 14, 8)
            }, 0, row);
            previewLayout.Controls.Add(value, 1, row);
        }

        _toolsPreviewThemeLabel = CreatePreviewValue();
        _toolsPreviewOverlayLabel = CreatePreviewValue();
        _toolsPreviewMonitoringLabel = CreatePreviewValue();
        _toolsPreviewDefaultsLabel = CreatePreviewValue();

        AddRow("Theme", _toolsPreviewThemeLabel, 0);
        AddRow("Overlay", _toolsPreviewOverlayLabel, 1);
        AddRow("Monitoring", _toolsPreviewMonitoringLabel, 2);
        AddRow("Defaults", _toolsPreviewDefaultsLabel, 3);

        previewSurface.Controls.Add(previewLayout);

        var helperFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };
        var explainButton = AppTheme.CreateButton("What do these mean?", width: 162);
        explainButton.Click += (_, _) => ShowToolsHelp(
            "Tools Preview",
            "Theme affects the app shell, Overlay affects the on-screen HUD, Monitoring controls telemetry intensity, and Defaults shape the universal Boost Now flow.");
        helperFlow.Controls.Add(explainButton);

        layout.Controls.Add(previewSurface, 0, 2);
        layout.Controls.Add(helperFlow, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateDiagnosticLabel()
    {
        return new Label
        {
            AutoSize = true,
            Font = AppTheme.CaptionFont(9.5f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 18, 10),
            Padding = new Padding(0, 4, 0, 4)
        };
    }

    private CardPanel BuildLegacyOverlaySettingsCard()
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

    private CardPanel BuildOverlaySettingsCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = AppTheme.Surface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        for (var i = 0; i < 7; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var headerFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = AppTheme.Surface
        };
        headerFlow.Controls.Add(new Label
        {
            Text = "Overlay Studio",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        });
        var helpButton = AppTheme.CreateButton("?", width: 42);
        helpButton.Margin = new Padding(10, 0, 0, 0);
        helpButton.Click += (_, _) => ShowToolsHelp(
            "Overlay Studio",
            "Use Competitive or Full Stats for fuller telemetry, Minimal for a cleaner stream-friendly HUD, and Custom when you want to tune colors and font yourself.");
        headerFlow.Controls.Add(helpButton);
        layout.Controls.Add(headerFlow, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Tune the live overlay without restarting. Minimal keeps the background fully transparent, while Card mode uses your custom tint.",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);

        Panel CreatePreview(Color color)
        {
            return new Panel
            {
                Width = 22,
                Height = 22,
                BackColor = Color.FromArgb(255, color.R, color.G, color.B),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 6, 6, 4),
                Cursor = Cursors.Hand
            };
        }

        Label CreateInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CaptionFont(9.5f),
                Padding = new Padding(0, 8, 6, 0)
            };
        }

        void MarkOverlayPresetAsCustom()
        {
            if (_isSyncingOverlayStudio)
            {
                return;
            }

            _settings.OverlayProfilePreset = OverlayProfilePreset.Custom;
            if (_overlayProfileComboBox is not null && _overlayProfileComboBox.SelectedItem is not OverlayProfilePreset.Custom)
            {
                _overlayProfileComboBox.SelectedItem = OverlayProfilePreset.Custom;
            }
        }

        FlowLayoutPanel CreateRow(int bottomMargin = 10)
        {
            return new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = AppTheme.Surface,
                Margin = new Padding(0, 0, 0, bottomMargin)
            };
        }

        CheckBox CreateMetricToggle(string text, bool initialValue, Action<bool> apply)
        {
            var checkBox = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = initialValue,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CaptionFont(9.5f),
                Padding = new Padding(0, 7, 14, 7)
            };
            checkBox.CheckedChanged += (_, _) =>
            {
                apply(checkBox.Checked);
                MarkOverlayPresetAsCustom();
                SaveSettings();
                RefreshToolsPreviewCard();
                RecreateOverlay();
            };

            return checkBox;
        }

        void PickOverlayColor(string title, Panel preview, Func<Color> getCurrentColor, Action<Color> applyColor, bool preserveAlpha = false)
        {
            using var dialog = new ColorDialog
            {
                Color = Color.FromArgb(255, getCurrentColor().R, getCurrentColor().G, getCurrentColor().B),
                FullOpen = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var current = getCurrentColor();
            var selected = preserveAlpha
                ? Color.FromArgb(current.A, dialog.Color.R, dialog.Color.G, dialog.Color.B)
                : dialog.Color;

            preview.BackColor = Color.FromArgb(255, selected.R, selected.G, selected.B);
            applyColor(selected);
            MarkOverlayPresetAsCustom();
            SaveSettings();
            RefreshToolsPreviewCard();
            RecreateOverlay();
            _logger.Log($"{title} updated.");
        }

        _overlayProfileComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 164,
            Margin = new Padding(0, 4, 18, 4)
        });
        foreach (var preset in Enum.GetValues<OverlayProfilePreset>())
        {
            _overlayProfileComboBox.Items.Add(preset);
        }
        var overlayProfileIndex = _overlayProfileComboBox.Items.IndexOf(_settings.OverlayProfilePreset);
        _overlayProfileComboBox.SelectedIndex = overlayProfileIndex >= 0 ? overlayProfileIndex : 0;
        _overlayProfileComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_isSyncingOverlayStudio)
            {
                return;
            }

            if (_overlayProfileComboBox.SelectedItem is not OverlayProfilePreset preset)
            {
                return;
            }

            ApplyOverlayProfilePreset(preset);
        };

        _overlayStyleComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 118,
            Margin = new Padding(0, 4, 18, 4)
        });
        _overlayStyleComboBox.Items.Add("Card");
        _overlayStyleComboBox.Items.Add("Minimal");
        _overlayStyleComboBox.SelectedIndex = _settings.OverlayStyle == OverlayStyle.Minimal ? 1 : 0;
        _overlayStyleComboBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.OverlayStyle = _overlayStyleComboBox.SelectedIndex == 1 ? OverlayStyle.Minimal : OverlayStyle.Card;
            MarkOverlayPresetAsCustom();
            SaveSettings();
            RefreshToolsPreviewCard();
            RecreateOverlay();
        };

        _overlayFontComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 162,
            Margin = new Padding(0, 4, 18, 4)
        });
        foreach (var preset in Enum.GetValues<OverlayFontPreset>())
        {
            _overlayFontComboBox.Items.Add(preset);
        }

        var fontIndex = _overlayFontComboBox.Items.IndexOf(_settings.OverlayFontPreset);
        _overlayFontComboBox.SelectedIndex = fontIndex >= 0 ? fontIndex : 0;
        _overlayFontComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_overlayFontComboBox.SelectedItem is not OverlayFontPreset preset)
            {
                return;
            }

            _settings.OverlayFontPreset = preset;
            MarkOverlayPresetAsCustom();
            SaveSettings();
            RefreshToolsPreviewCard();
            RecreateOverlay();
        };

        _overlayShowFpsCheckBox = CreateMetricToggle("FPS", _settings.OverlayShowFps, value => _settings.OverlayShowFps = value);
        _overlayShowCpuCheckBox = CreateMetricToggle("CPU", _settings.OverlayShowCpu, value => _settings.OverlayShowCpu = value);
        _overlayShowGpuCheckBox = CreateMetricToggle("GPU", _settings.OverlayShowGpu, value => _settings.OverlayShowGpu = value);

        _overlayAccentPreview = CreatePreview(Color.FromArgb(_settings.OverlayAccentArgb));
        _overlayTextPreview = CreatePreview(Color.FromArgb(_settings.OverlayTextArgb));
        _overlayBackgroundPreview = CreatePreview(Color.FromArgb(_settings.OverlayBackgroundArgb));

        var presetRow = CreateRow();
        presetRow.Controls.Add(CreateInlineLabel("Profile"));
        presetRow.Controls.Add(_overlayProfileComboBox);

        var styleRow = CreateRow();
        styleRow.Controls.Add(CreateInlineLabel("Style"));
        styleRow.Controls.Add(_overlayStyleComboBox);
        styleRow.Controls.Add(CreateInlineLabel("Font"));
        styleRow.Controls.Add(_overlayFontComboBox);

        var metricsRow = CreateRow();
        metricsRow.Controls.Add(CreateInlineLabel("Show"));
        metricsRow.Controls.Add(_overlayShowFpsCheckBox);
        metricsRow.Controls.Add(_overlayShowCpuCheckBox);
        metricsRow.Controls.Add(_overlayShowGpuCheckBox);

        var colorsRow = CreateRow(6);

        var accentButton = AppTheme.CreateButton("Accent color", width: 132);
        accentButton.Margin = new Padding(0, 4, 12, 4);
        accentButton.Click += (_, _) => PickOverlayColor(
            "Overlay accent color",
            _overlayAccentPreview,
            () => Color.FromArgb(_settings.OverlayAccentArgb),
            color => _settings.OverlayAccentArgb = color.ToArgb());

        var textButton = AppTheme.CreateButton("Text color", width: 128);
        textButton.Margin = new Padding(0, 4, 12, 4);
        textButton.Click += (_, _) => PickOverlayColor(
            "Overlay text color",
            _overlayTextPreview,
            () => Color.FromArgb(_settings.OverlayTextArgb),
            color => _settings.OverlayTextArgb = color.ToArgb());

        var backgroundButton = AppTheme.CreateButton("Card tint", width: 122);
        backgroundButton.Margin = new Padding(0, 4, 0, 4);
        backgroundButton.Click += (_, _) => PickOverlayColor(
            "Overlay card tint",
            _overlayBackgroundPreview,
            () => Color.FromArgb(_settings.OverlayBackgroundArgb),
            color => _settings.OverlayBackgroundArgb = color.ToArgb(),
            preserveAlpha: true);

        colorsRow.Controls.Add(_overlayAccentPreview);
        colorsRow.Controls.Add(accentButton);
        colorsRow.Controls.Add(_overlayTextPreview);
        colorsRow.Controls.Add(textButton);
        colorsRow.Controls.Add(_overlayBackgroundPreview);
        colorsRow.Controls.Add(backgroundButton);

        var tipLabel = new Label
        {
            Text = "Tip: use Card style for a premium HUD panel, or Minimal for a cleaner streamer-style readout. Background tint only shows while Card mode is active.",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(9f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 4, 0, 0)
        };

        layout.Controls.Add(presetRow, 0, 2);
        layout.Controls.Add(styleRow, 0, 3);
        layout.Controls.Add(metricsRow, 0, 4);
        layout.Controls.Add(colorsRow, 0, 5);
        layout.Controls.Add(tipLabel, 0, 6);
        card.Controls.Add(layout);
        SyncOverlayStudioControls();
        return card;
    }

    private CardPanel BuildThemeSettingsCard()
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            CornerRadius = 18,
            EnableSpotlight = false,
            Margin = new Padding(0, 0, 0, 14),
            InnerPadding = new Padding(18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = AppTheme.Surface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = AppTheme.Surface
        };
        headerFlow.Controls.Add(new Label
        {
            Text = "Theme Studio",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        });
        var helpButton = AppTheme.CreateButton("?", width: 42);
        helpButton.Margin = new Padding(10, 0, 0, 0);
        helpButton.Click += (_, _) => ShowToolsHelp(
            "Theme Studio",
            "Themes recolor the CloudFrame shell. Graphite is the balanced default, Midnight is deeper and more dramatic, Ember is warmer, and Frost is lighter and cooler.");
        headerFlow.Controls.Add(helpButton);
        layout.Controls.Add(headerFlow, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Pick a shell theme for CloudFrame. Restart to apply the full app chrome cleanly so every surface, card, and tab stays consistent.",
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
            BackColor = AppTheme.Surface,
            MaximumSize = new Size(1080, 0),
            Margin = new Padding(0)
        };

        flow.Controls.Add(new Label
        {
            Text = "App theme:",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 7, 6, 0)
        });

        _themePresetComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 168,
            Margin = new Padding(0, 4, 16, 4)
        });
        foreach (var preset in Enum.GetValues<AppThemePreset>())
        {
            _themePresetComboBox.Items.Add(preset);
        }

        var themeIndex = _themePresetComboBox.Items.IndexOf(_settings.ThemePreset);
        _themePresetComboBox.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;
        _themePresetComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_themePresetComboBox.SelectedItem is not AppThemePreset preset || preset == _settings.ThemePreset)
            {
                return;
            }

            _settings.ThemePreset = preset;
            SaveSettings();
            RefreshToolsPreviewCard();

            if (MessageBox.Show(
                    this,
                    "Theme saved. Restart CloudFrame now to apply the full shell theme cleanly?",
                    "CloudFrame",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
                Close();
            }
        };

        flow.Controls.Add(_themePresetComboBox);
        layout.Controls.Add(flow, 0, 2);
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
            EnableSpotlight = false,
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

        var headerFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = AppTheme.Surface
        };
        headerFlow.Controls.Add(new Label
        {
            Text = "Session Defaults",
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary
        });
        var helpButton = AppTheme.CreateButton("?", width: 42);
        helpButton.Margin = new Padding(10, 0, 0, 0);
        helpButton.Click += (_, _) => ShowToolsHelp(
            "Session Defaults",
            "These are the global defaults CloudFrame uses for Boost Now and the general launch experience. Saved profile settings still take priority over these.");
        headerFlow.Controls.Add(helpButton);
        layout.Controls.Add(headerFlow, 0, 0);
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
                RequestToolsUiRefresh();
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

        _minimizeToTrayCheckBox = MakeDefaultToggle(
            "Minimize to tray",
            _settings.MinimizeToTray,
            v =>
            {
                _settings.MinimizeToTray = v;
                if (!v && _trayIcon is not null)
                {
                    _trayIcon.Visible = false;
                    ShowInTaskbar = true;
                }
            },
            "Keeps the overlay running while the main CloudFrame window hides to the system tray when minimized.");
        flow.Controls.Add(_minimizeToTrayCheckBox);

        flow.Controls.Add(MakeDefaultToggle(
            "Use maintenance backoff",
            _settings.EnableMaintenanceBackoff,
            v => _settings.EnableMaintenanceBackoff = v,
            "If CloudFrame sees a sudden live FPS dip under CPU pressure, it briefly backs recurring maintenance off instead of hammering the game."));

        var actionsFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = AppTheme.Surface,
            MaximumSize = new Size(1080, 0),
            Margin = new Padding(0, 10, 0, 0)
        };
        var checkUpdatesButton = AppTheme.CreateButton("Check for Updates Now", width: 182);
        checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync(showUpToDateMessage: true);
        actionsFlow.Controls.Add(checkUpdatesButton);
        actionsFlow.Controls.Add(new Label
        {
            Text = "Monitoring mode:",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9f),
            Padding = new Padding(8, 9, 8, 0),
            BackColor = AppTheme.Surface
        });
        _monitoringModeComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 136
        });
        foreach (var mode in Enum.GetValues<MonitoringMode>())
        {
            _monitoringModeComboBox.Items.Add(mode);
        }
        _monitoringModeComboBox.SelectedItem = _settings.MonitoringMode;
        _monitoringModeComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_monitoringModeComboBox.SelectedItem is not MonitoringMode mode)
            {
                return;
            }

            _settings.MonitoringMode = mode;
            SaveSettings();
            ApplyMonitoringMode();
            RequestToolsUiRefresh();
            _logger.Log($"Monitoring mode set to {mode}.");
        };
        actionsFlow.Controls.Add(_monitoringModeComboBox);
        actionsFlow.Controls.Add(new Label
        {
            Text = $"Release feed: {_settings.GitHubRepository}",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9f),
            Padding = new Padding(8, 9, 0, 0),
            BackColor = AppTheme.Surface
        });

        layout.Controls.Add(flow, 0, 2);
        layout.Controls.Add(actionsFlow, 0, 3);
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
            EnableSpotlight = false,
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

        var headerFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = AppTheme.Surface
        };
        headerFlow.Controls.Add(new Label
        {
            Text = "Boost Engine", AutoSize = true,
            Font = AppTheme.TitleFont(14f), ForeColor = AppTheme.TextPrimary
        });
        var helpButton = AppTheme.CreateButton("?", width: 42);
        helpButton.Margin = new Padding(10, 0, 0, 0);
        helpButton.Click += (_, _) => ShowToolsHelp(
            "Boost Engine",
            "These are the deeper system-side switches behind CloudFrame's presets. They stay reversible and should only be adjusted if you want to tune how aggressive the boost engine feels.");
        headerFlow.Controls.Add(helpButton);
        layout.Controls.Add(headerFlow, 0, 0);
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
        Resize += (_, _) => HandleWindowResize();
    }

    private async Task InitializeAsync()
    {
        _logger.Log("CloudFrame main window shown.");
        _logger.Log("Startup step: UpdateStatusLabels");
        UpdateStatusLabels();
        _logger.Log("Startup step: UpdateShaderCacheLabel");
        UpdateShaderCacheLabel();
        _logger.Log("Startup step: RecoverPendingSessionAsync");
        await _boostCoordinator.RecoverPendingSessionAsync(_settings.Profiles);
        _logger.Log("Startup step: ToggleOverlay");
        ToggleOverlay(_overlayToggleCheckBox?.Checked == true);
        _logger.Log("Startup step: LoadPowerPlansAsync queued");
        _ = Task.Run(LoadPowerPlansAsync);

        await Task.Delay(120);
        if (IsDisposed || Disposing)
        {
            return;
        }

        _startupReady = true;
        _logger.Log("Startup step: UpdateAnimatedUiState");
        UpdateAnimatedUiState();
        _logger.Log("Startup step: UpdateFpsTrackingTarget");
        UpdateFpsTrackingTarget();
        _logger.Log("Startup step: Telemetry timer start");
        _telemetryTimer.Start();
        _logger.Log("Startup step: ScheduleTelemetryRefresh");
        ScheduleTelemetryRefresh();
        _logger.Log("CloudFrame startup warmup complete.");
        BeginInvoke(new Action(() =>
        {
            MaybeRunFirstRunSetup();
            _ = CheckForUpdatesAsync(showUpToDateMessage: false);
            QueueDeferredTabWarmup();
        }));
    }

    private void HandleWindowResize()
    {
        if (!_settings.MinimizeToTray || _restoringFromTray || _trayIcon is null)
        {
            return;
        }

        if (WindowState != FormWindowState.Minimized || !Visible)
        {
            return;
        }

        SendToTray("CloudFrame minimized to the tray.");
    }

    private void RestoreFromTray()
    {
        if (_trayIcon is null || IsDisposed || Disposing)
        {
            return;
        }

        _restoringFromTray = true;
        try
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            _trayIcon.Visible = false;
        }
        finally
        {
            _restoringFromTray = false;
        }
    }

    private void SendToTray(string logMessage)
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
        _logger.Log(logMessage);
    }

    private async Task LoadPowerPlansAsync()
    {
        var plans = await _powerPlanService.GetPlansAsync();
        _powerPlans.Clear();
        _powerPlans.AddRange(plans);

        if (_powerPlanList is not null && !IsDisposed && !Disposing)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(BindLoadedPowerPlans));
            }
            else
            {
                BindLoadedPowerPlans();
            }
        }

        RequestToolsUiRefresh();
    }

    private void BindLoadedPowerPlans()
    {
        if (_powerPlanList is null || _powerPlanList.IsDisposed)
        {
            return;
        }

        _powerPlanList.DataSource = null;
        _powerPlanList.DataSource = _powerPlans.ToList();
    }

    private void BindProfiles()
    {
        if (_profilesList is null || _profileCountLabel is null)
        {
            return;
        }

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
            if (_profilesList is null)
            {
                return null;
            }

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

    private void RefreshProfileStudioCard()
    {
        if (_profileSnapshotCard is null)
        {
            return;
        }

        if (!IsProfilesTabActive())
        {
            _profileStudioDirty = true;
            return;
        }

        RefreshProfileStudioCardCore();
    }

    private void RefreshProfileStudioCardCore()
    {
        if (_profileSnapshotCard is null)
        {
            return;
        }

        _profileSnapshotCard.SuspendLayout();
        var profile = SelectedProfile;
        _profileSnapshotCard.SetProfile(profile);
        _profileSnapshotCard.SetContext(
            _lastSessionReport,
            profile is null ? null : GetStoredRecommendationMemory(profile),
            _latestTelemetry,
            _lastLiveSession is not null,
            _settings.EnableUpdateChecks);
        _profileSnapshotCard.ResumeLayout(true);
        _profileStudioDirty = false;
    }

    private void RefreshDashboardUiCore()
    {
        if (_cpuRing is not null)
        {
            _cpuRing.Value = (float)_latestTelemetry.CpuPercent;
        }

        if (_gpuRing is not null)
        {
            _gpuRing.Value = (float)(_latestTelemetry.GpuPercent ?? 0);
        }

        if (_fpsRing is not null && _latestTelemetry.FramesPerSecond is double fps)
        {
            _fpsRing.Value = (float)Math.Min(fps, 300);
        }

        UpdateDashboardCards();
        _dashboardUiDirty = false;
    }

    private void UpdateAnimatedUiState()
    {
        var selectedTabText = _mainTabs?.SelectedTab?.Text ?? string.Empty;
        var dashboardActive = string.Equals(selectedTabText, "Dashboard", StringComparison.OrdinalIgnoreCase);
        var profilesActive = string.Equals(selectedTabText, "Profiles", StringComparison.OrdinalIgnoreCase);
        var toolsActive = string.Equals(selectedTabText, "Tools", StringComparison.OrdinalIgnoreCase);
        _dashboardBackdropPanel?.SetAnimationEnabled(dashboardActive);
        _profileSnapshotCard?.SetAnimationEnabled(profilesActive);
        if (dashboardActive && _dashboardUiDirty)
        {
            QueueDashboardUiRefresh();
        }
        if (profilesActive && _profileStudioDirty)
        {
            QueueProfileStudioRefresh();
        }
        if (toolsActive && _toolsUiDirty)
        {
            QueueToolsUiRefresh();
        }
    }

    private void RequestProfileStudioRefresh()
    {
        _profileStudioDirty = true;
        if (IsProfilesTabActive())
        {
            QueueProfileStudioRefresh();
        }
    }

    private bool IsProfilesTabActive()
        => string.Equals(_mainTabs?.SelectedTab?.Text, "Profiles", StringComparison.OrdinalIgnoreCase);

    private bool IsDashboardTabActive()
        => string.Equals(_mainTabs?.SelectedTab?.Text, "Dashboard", StringComparison.OrdinalIgnoreCase);

    private void RefreshToolsUiCore()
    {
        UpdateToolsSelectionInfo();
        UpdateReadinessDiagnostics();
        RefreshToolsPreviewCardCore();
        _toolsUiDirty = false;
    }

    private void RequestToolsUiRefresh()
    {
        _toolsUiDirty = true;
        if (IsToolsTabActive())
        {
            QueueToolsUiRefresh();
        }
    }

    private bool IsToolsTabActive()
        => string.Equals(_mainTabs?.SelectedTab?.Text, "Tools", StringComparison.OrdinalIgnoreCase);

    private bool IsResolutionTabActive()
        => string.Equals(_mainTabs?.SelectedTab?.Text, "Resolution", StringComparison.OrdinalIgnoreCase);

    private void RequestDashboardUiRefresh()
    {
        _dashboardUiDirty = true;
        if (IsDashboardTabActive())
        {
            QueueDashboardUiRefresh();
        }
    }

    private void QueueProfileStudioRefresh()
    {
        if (_profileRefreshQueued || IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        _profileRefreshQueued = true;
        BeginInvoke(new Action(() =>
        {
            _profileRefreshQueued = false;
            if (!IsDisposed && !Disposing)
            {
                RefreshProfileStudioCardCore();
            }
        }));
    }

    private void QueueToolsUiRefresh()
    {
        if (_toolsRefreshQueued || IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        _toolsRefreshQueued = true;
        BeginInvoke(new Action(() =>
        {
            _toolsRefreshQueued = false;
            if (!IsDisposed && !Disposing)
            {
                RefreshToolsUiCore();
            }
        }));
    }

    private void QueueDashboardUiRefresh()
    {
        if (_dashboardRefreshQueued || IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        _dashboardRefreshQueued = true;
        BeginInvoke(new Action(() =>
        {
            _dashboardRefreshQueued = false;
            if (!IsDisposed && !Disposing)
            {
                RefreshDashboardUiCore();
            }
        }));
    }

    private void ApplyToolsPreset(
        AppThemePreset themePreset,
        OverlayProfilePreset overlayPreset,
        MonitoringMode monitoringMode,
        bool updateChecks,
        bool recurringMaintenance,
        bool memoryPriority,
        bool ecoQos)
    {
        _settings.ThemePreset = themePreset;
        _settings.EnableUpdateChecks = updateChecks;
        _settings.MonitoringMode = monitoringMode;
        _settings.UniversalEnableRecurringMaintenance = recurringMaintenance;
        _settings.UniversalUseBackgroundMemoryPriority = memoryPriority;
        _settings.UniversalUseBackgroundEcoQos = ecoQos;

        if (_themePresetComboBox is not null)
        {
            _themePresetComboBox.SelectedItem = themePreset;
        }

        if (_monitoringModeComboBox is not null)
        {
            _monitoringModeComboBox.SelectedItem = monitoringMode;
        }

        ApplyOverlayProfilePreset(overlayPreset);
    }

    private void RefreshToolsPreviewCard()
    {
        _toolsUiDirty = true;
        if (IsToolsTabActive())
        {
            RefreshToolsPreviewCardCore();
        }
    }

    private void RefreshToolsPreviewCardCore()
    {
        if (_toolsPreviewThemeLabel is null)
        {
            return;
        }

        _toolsPreviewThemeLabel.Text = $"{_settings.ThemePreset} shell palette";
        _toolsPreviewOverlayLabel.Text = $"{_settings.OverlayProfilePreset} / {_settings.OverlayStyle} / {_settings.OverlayFontPreset}";
        _toolsPreviewMonitoringLabel.Text = $"{_settings.MonitoringMode} telemetry";
        _toolsPreviewDefaultsLabel.Text = $"{(_settings.EnableUpdateChecks ? "Auto-update checks on" : "Manual updates")} | {(_settings.UniversalEnableRecurringMaintenance ? "maintenance on" : "maintenance off")} | {(_settings.MinimizeToTray ? "tray mode ready" : "taskbar mode")}";
    }

    private void UpdateDashboardAdvancedMode()
    {
        var advanced = _advancedViewCheckBox?.Checked == true;
        if (_dashboardGaugePanel is not null) _dashboardGaugePanel.Visible = advanced;
        if (_dashboardPerformanceLabPanel is not null) _dashboardPerformanceLabPanel.Visible = advanced;
        if (_dashboardSessionReportPanel is not null) _dashboardSessionReportPanel.Visible = advanced;
        if (_dashboardRecommendationPanel is not null) _dashboardRecommendationPanel.Visible = advanced;
        if (_dashboardLogGroup is not null) _dashboardLogGroup.Visible = advanced;
    }

    private void ShowToolsHelp(string title, string body)
    {
        MessageBox.Show(this, body, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private void ApplyMonitoringMode()
    {
        _telemetryTimer.Interval = _settings.MonitoringMode switch
        {
            MonitoringMode.Minimal => 2400,
            MonitoringMode.Detailed => 900,
            _ => 1500
        };
    }

    private async Task CheckForUpdatesAsync(bool showUpToDateMessage)
    {
        if (_isCheckingForUpdates || !_settings.EnableUpdateChecks && !showUpToDateMessage)
        {
            return;
        }

        _isCheckingForUpdates = true;
        SetTransientExperienceStatus("Checking for updates from the CloudFrame release feed...", context: "Updates", durationMs: 1800);
        try
        {
            var result = await _updateCheckerService.CheckForUpdateAsync(
                _settings.GitHubRepository,
                _settings.SkippedUpdateVersion);

            if (result.IsUpdateAvailable)
            {
                SetTransientExperienceStatus($"CloudFrame {result.LatestVersion} is ready to review.", context: "Updates");
                ShowUpdatePrompt(result);
                return;
            }

            if (showUpToDateMessage)
            {
                MessageBox.Show(this, result.Message, "CloudFrame Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            _logger.Log(result.Message);
            SetTransientExperienceStatus(result.Message, context: "Updates");
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void ShowUpdatePrompt(UpdateCheckResult result)
    {
        using var prompt = new UpdatePromptForm(result);
        prompt.ShowDialog(this);

        switch (prompt.Choice)
        {
            case UpdatePromptChoice.InstallNow:
                _ = InstallUpdateAsync(result);
                break;
            case UpdatePromptChoice.OpenRelease:
                OpenExternalPath(result.ReleaseUrl);
                _logger.Log($"Opened CloudFrame release page for {result.LatestVersion}.");
                SetTransientExperienceStatus($"Opened the CloudFrame {result.LatestVersion} release page.", context: "Updates");
                break;
            case UpdatePromptChoice.SkipVersion:
                _settings.SkippedUpdateVersion = result.LatestVersion;
                SaveSettings();
                _logger.Log($"Skipped update prompt for CloudFrame {result.LatestVersion}.");
                SetTransientExperienceStatus($"CloudFrame {result.LatestVersion} will be skipped until a newer release appears.", context: "Updates");
                break;
            case UpdatePromptChoice.Later:
            default:
                _logger.Log($"Deferred update prompt for CloudFrame {result.LatestVersion}.");
                SetTransientExperienceStatus($"CloudFrame {result.LatestVersion} was left for later.", context: "Updates");
                break;
        }
    }

    private async Task InstallUpdateAsync(UpdateCheckResult result)
    {
        if (_boostCoordinator.ActiveSession is not null)
        {
            MessageBox.Show(
                this,
                "Restore the current boost session before installing an update. CloudFrame will not update while a boost session is active.",
                "CloudFrame Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            Enabled = false;

            var prepared = await _updateInstallerService.PrepareUpdateAsync(result);
            if (prepared is null)
            {
                MessageBox.Show(
                    this,
                    "CloudFrame found the release, but could not prepare the update package automatically.",
                    "CloudFrame Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                $"CloudFrame {result.LatestVersion} is ready to install.\r\n\r\nThe app will close, apply the update, and restart automatically.",
                "CloudFrame Update",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);

            if (confirm != DialogResult.OK)
            {
                _logger.Log($"Prepared CloudFrame {result.LatestVersion}, but the user chose not to restart yet.");
                return;
            }

            _logger.Log($"Prepared CloudFrame {result.LatestVersion}. Restarting to apply the update.");
            _updateInstallerService.LaunchInstallerAndExit(prepared);
            Close();
        }
        catch (Exception ex)
        {
            _logger.Log($"Automatic update failed: {ex.Message}");
            MessageBox.Show(
                this,
                $"CloudFrame could not apply the update automatically.\r\n\r\n{ex.Message}",
                "CloudFrame Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                Enabled = true;
                Cursor = Cursors.Default;
            }
        }
    }

    private void MaybeRunFirstRunSetup()
    {
        if (_settings.HasCompletedFirstRunSetup || _settings.Profiles.Count > 0)
        {
            return;
        }

        using var dialog = new FirstRunSetupForm(GetRunningProcessEntries);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            _settings.HasCompletedFirstRunSetup = true;
            SaveSettings();
            return;
        }

        foreach (var profile in dialog.ProfilesToCreate)
        {
            _settings.Profiles.Add(profile);
        }

        _settings.MinimizeToTray = dialog.EnableTrayMode;
        _settings.OverlayProfilePreset = dialog.OverlayPreset;
        ApplyOverlayProfilePreset(dialog.OverlayPreset);
        _settings.HasCompletedFirstRunSetup = true;
        SaveSettings();
        BindProfiles();
        _ = ManualScanNowAsync();
        _logger.Log($"Created {dialog.ProfilesToCreate.Count} starter profile(s) from the first-run setup.");
    }

    private void ExportIssueReport()
    {
        var reportPath = _issueReportService.ExportIssueReport(
            _settings,
            _boostCoordinator.ActiveSession,
            _latestTelemetry,
            _lastSessionReport,
            _currentRecommendation,
            _detectedGames);

        _logger.Log($"Exported issue report to '{reportPath}'.");
        OpenExternalPath(reportPath);
    }

    private void CopyPerformanceLabSnapshot()
    {
        var delta = _fpsComparisonTracker.GetSnapshot();
        var lines = new List<string>
        {
            "CloudFrame Performance Lab Snapshot",
            $"Captured: {DateTime.Now:G}",
            $"Selected profile: {SelectedProfile?.Name ?? "None"}",
            $"Active session: {_boostCoordinator.ActiveSession?.Profile.Name ?? "None"}"
        };

        lines.Add(delta.BaselineMetrics is not null
            ? $"Baseline: {FormatPerformanceMetrics(delta.BaselineMetrics.Value)}"
            : "Baseline: waiting for enough profiled-game idle samples.");

        if (delta.LiveMetrics is not null)
        {
            lines.Add($"Live: {FormatPerformanceMetrics(delta.LiveMetrics.Value)}");
            lines.Add($"Session quality: {DescribePacing(delta.LiveMetrics.Value.PacingScore)} | Avg frame time {delta.LiveMetrics.Value.AverageFrameTimeMs:0.00} ms | P95 {delta.LiveMetrics.Value.P95FrameTimeMs:0.00} ms");
        }
        else
        {
            lines.Add("Live: waiting for enough boosted-session samples.");
        }

        if (delta.HasResult)
        {
            var avgSign = (delta.DeltaPercent ?? 0) >= 0 ? "+" : string.Empty;
            var lowSign = (delta.DeltaOnePercentLowFps ?? 0) >= 0 ? "+" : string.Empty;
            lines.Add($"Measured delta: {avgSign}{delta.DeltaPercent:0.#}% avg | {lowSign}{delta.DeltaOnePercentLowFps:0.#} FPS 1% low");
        }

        if (_lastSessionReport is not null)
        {
            lines.Add($"Last session: {_lastSessionReport.ProfileName} | {_lastSessionReport.Summary}");
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            SetTransientExperienceStatus("Performance Lab snapshot copied to the clipboard.", context: "Performance Lab");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to copy Performance Lab snapshot: {ex.Message}");
            SetTransientExperienceStatus($"CloudFrame could not copy the Performance Lab snapshot: {ex.Message}", warning: true, context: "Performance Lab");
        }
    }

    private async Task ManualScanNowAsync()
    {
        _logger.Log("Manual process scan started.");
        SetTransientExperienceStatus("Scanning live running processes from your current Windows session...", context: "Dashboard scan", durationMs: 2200);

        IReadOnlyList<RunningProcessEntry> entries;
        try
        {
            entries = await Task.Run(() => _windowResolutionService.ListDashboardProcesses());
        }
        catch (Exception ex)
        {
            _logger.Log($"Manual process scan failed: {ex.Message}");
            SetTransientExperienceStatus($"CloudFrame could not scan running processes: {ex.Message}", warning: true, context: "Dashboard scan");
            return;
        }

        _scannedProcesses.Clear();
        _scannedProcesses.AddRange(entries);

        _logger.Log($"Manual process scan collected {_scannedProcesses.Count} process candidate(s).");
        UpdateStatusLabels();
        RefreshDetectedGrid();

        if (_scannedProcesses.Count == 0)
        {
            SetTransientExperienceStatus("No runnable user-session processes were found. Launch an app first, then scan again.", warning: true, context: "Dashboard scan");
            return;
        }

        var matchedCount = BuildDashboardScanEntries(string.Empty).Count(static entry => entry.HasProfileMatch);
        SetTransientExperienceStatus(
            $"Scanned {_scannedProcesses.Count} live process(es). {matchedCount} matched a saved CloudFrame profile; everything else is ready for universal quick boost.",
            context: "Dashboard scan");
    }

    private void ClearDetectedMatches()
    {
        _detectedGames.Clear();
        _scannedProcesses.Clear();
        _logger.Log("Cleared scanned processes and running matches from the dashboard.");
        UpdateStatusLabels();
        RefreshDetectedGrid();
    }

    private async Task BoostSelectedGameAsync()
    {
        if (_detectedGrid.CurrentRow?.Tag is not DashboardScanEntry selectedEntry)
        {
            MessageBox.Show(this, "Select a scanned process first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var boostProfile = selectedEntry.Profile ?? CreateUniversalBoostProfile();
        var result = await _boostCoordinator.ApplyBoostAsync(boostProfile, selectedEntry.BoostProcessId, false);
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
            var result = await _boostCoordinator.ApplyBoostAsync(profile, matches[0].BoostProcessId, false);
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

        var selectedResult = await _boostCoordinator.ApplyBoostAsync(profile, selected.BoostProcessId, false);
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
        _ = ManualScanNowAsync();
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
        var entries = BuildDashboardScanEntries(filter);
        UpdateDashboardScanSummary(entries, filter);

        foreach (var entry in entries)
        {
            var rowIndex = _detectedGrid.Rows.Add(
                entry.DisplayProfileName,
                entry.ProcessName,
                entry.ProcessId,
                entry.Status,
                entry.ExecutablePath);

            _detectedGrid.Rows[rowIndex].Tag = entry;
        }
    }

    private void UpdateDashboardScanSummary(IReadOnlyList<DashboardScanEntry> entries, string filter)
    {
        if (_dashboardScanSummaryLabel is null)
        {
            return;
        }

        if (_scannedProcesses.Count == 0)
        {
            _dashboardScanSummaryLabel.Text = "Press Scan Now to inspect live running processes. Saved profiles help, but they are optional for a quick boost.";
            _dashboardScanSummaryLabel.ForeColor = AppTheme.TextSecondary;
            return;
        }

        var matchedCount = entries.Count(static entry => entry.HasProfileMatch);
        var visibleCount = _scannedProcesses.Count(static process => process.HasVisibleWindow);
        var groupedCount = entries.Count;
        var rawCount = _scannedProcesses.Count;

        if (entries.Count == 0)
        {
            _dashboardScanSummaryLabel.Text = string.IsNullOrWhiteSpace(filter)
                ? "CloudFrame completed the scan, but nothing from the current Windows session could be listed. Try again after launching the target app."
                : $"No scanned processes matched the current filter \"{filter}\".";
            _dashboardScanSummaryLabel.ForeColor = AppTheme.Warning;
            return;
        }

        var visibilityText = visibleCount > 0
            ? $"{visibleCount} with live windows"
            : "background-only process entries";

        _dashboardScanSummaryLabel.Text = $"Showing {groupedCount} grouped process{(groupedCount == 1 ? string.Empty : "es")} from {rawCount} running process{(rawCount == 1 ? string.Empty : "es")} | {matchedCount} matched profile{(matchedCount == 1 ? string.Empty : "s")} | {visibilityText}.";
        _dashboardScanSummaryLabel.ForeColor = entries.Count > 0 ? AppTheme.Success : AppTheme.AccentSoft;
    }

    private List<DashboardScanEntry> BuildDashboardScanEntries(string filter)
    {
        var entries = new List<DashboardScanEntry>(_scannedProcesses.Count);
        foreach (var group in _scannedProcesses
                     .GroupBy(static process => BuildDashboardProcessGroupKey(process), StringComparer.OrdinalIgnoreCase))
        {
            var process = group
                .OrderByDescending(static candidate => candidate.HasVisibleWindow)
                .ThenByDescending(static candidate => !string.IsNullOrWhiteSpace(candidate.WindowTitle))
                .ThenByDescending(static candidate => candidate.ProcessId)
                .First();
            var instanceCount = group.Count();
            var matchedGame = FindDetectedGameForProcess(process);
            var profile = matchedGame?.Profile ?? FindExactProfileForProcess(process);
            var status = BuildDashboardScanStatus(process, matchedGame, profile);
            var engineHint = matchedGame?.EngineHint
                ?? (profile is not null ? _processService.GetEngineHint(profile, process.ExecutablePath, process.ProcessName) : null);

            var entry = new DashboardScanEntry
            {
                Profile = profile,
                MatchedGame = matchedGame,
                ProcessId = matchedGame?.ProcessId ?? process.ProcessId,
                ProcessName = matchedGame?.ProcessName ?? process.ProcessName,
                WindowTitle = process.WindowTitle,
                ExecutablePath = matchedGame?.ExecutablePath ?? process.ExecutablePath,
                EngineHint = engineHint,
                Status = instanceCount > 1 ? $"{status} / {instanceCount} instances" : status,
                InstanceCount = instanceCount
            };

            if (MatchesDashboardScanEntryFilter(entry, filter))
            {
                entries.Add(entry);
            }
        }

        return entries
            .OrderByDescending(static entry => entry.HasProfileMatch)
            .ThenBy(static entry => entry.DisplayProfileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDashboardProcessGroupKey(RunningProcessEntry process)
    {
        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            return process.ExecutablePath;
        }

        return process.ProcessName;
    }

    private DetectedGame? FindDetectedGameForProcess(RunningProcessEntry process)
    {
        return _detectedGames.FirstOrDefault(game =>
            game.ProcessId == process.ProcessId
            || game.AnchorProcessId == process.ProcessId
            || (!string.IsNullOrWhiteSpace(game.ExecutablePath)
                && string.Equals(game.ExecutablePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(game.ProcessName, process.ProcessName, StringComparison.OrdinalIgnoreCase));
    }

    private GameProfile? FindExactProfileForProcess(RunningProcessEntry process)
    {
        return _settings.Profiles.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            && string.Equals(profile.ExecutablePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            ?? _settings.Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(profile.ExecutableName)
                && string.Equals(profile.ExecutableName, process.ProcessName, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildDashboardScanStatus(RunningProcessEntry process, DetectedGame? matchedGame, GameProfile? profile)
    {
        if (matchedGame is not null)
        {
            var status = _boostCoordinator.ActiveSession?.RecoveryState.GameProcessId == matchedGame.ProcessId
                ? BuildSessionStatus(_boostCoordinator.ActiveSession)
                : matchedGame.HasLauncherHandoff
                    ? $"Matched / via {matchedGame.AnchorProcessName ?? "launcher"}"
                    : "Matched / ready";

            if (!string.IsNullOrWhiteSpace(matchedGame.EngineHint))
            {
                status += $" / {matchedGame.EngineHint}";
            }

            return status;
        }

        if (profile is not null)
        {
            return "Profile match / ready to boost";
        }

        return string.IsNullOrWhiteSpace(process.WindowTitle)
            ? "Ready / universal boost"
            : $"Ready / universal boost / {process.WindowTitle}";
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
        RequestProfileStudioRefresh();
        RequestDashboardUiRefresh();
        RequestToolsUiRefresh();
        UpdateFrameGenLabStatus();
        UpdateExperienceStatusFromState();
    }

    private void UpdateExperienceStatusFromState()
    {
        if (_experienceStatusTitleLabel is null || _experienceStatusDetailLabel is null || _experienceStatusContextLabel is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_transientExperienceMessage))
        {
            _experienceStatusTitleLabel.Text = _transientExperienceWarning ? "Needs attention" : "Action complete";
            _experienceStatusTitleLabel.ForeColor = _transientExperienceWarning ? AppTheme.Warning : AppTheme.Success;
            _experienceStatusDetailLabel.Text = _transientExperienceMessage;
            _experienceStatusDetailLabel.ForeColor = _transientExperienceWarning ? AppTheme.TextPrimary : AppTheme.TextSecondary;
            _experienceStatusContextLabel.Text = string.IsNullOrWhiteSpace(_transientExperienceContext)
                ? BuildExperienceContext()
                : _transientExperienceContext!;
            return;
        }

        var session = _boostCoordinator.ActiveSession;
        var selectedProfile = SelectedProfile;
        if (session is not null)
        {
            _experienceStatusTitleLabel.Text = session.RecoveryState.IsPreLaunchBoost
                ? "Universal boost armed"
                : $"Boosting {session.Profile.Name}";
            _experienceStatusTitleLabel.ForeColor = AppTheme.Success;
            _experienceStatusDetailLabel.Text = session.RecoveryState.IsPreLaunchBoost
                ? "CloudFrame is holding your preferred boost posture and waiting for the actual game launch."
                : "CloudFrame is actively maintaining the live session with the current profile and safety rules.";
        }
        else if (selectedProfile is not null)
        {
            _experienceStatusTitleLabel.Text = $"{selectedProfile.Name} ready";
            _experienceStatusTitleLabel.ForeColor = AppTheme.TextPrimary;
            _experienceStatusDetailLabel.Text = "The selected profile is loaded and ready for boost, launch, resolution control, and prototype testing.";
        }
        else
        {
            _experienceStatusTitleLabel.Text = "CloudFrame ready";
            _experienceStatusTitleLabel.ForeColor = AppTheme.TextPrimary;
            _experienceStatusDetailLabel.Text = "Pick a profile, scan a running game, or use a quick action to start shaping the session.";
        }

        _experienceStatusDetailLabel.ForeColor = AppTheme.TextSecondary;
        _experienceStatusContextLabel.Text = BuildExperienceContext();
    }

    private string BuildExperienceContext()
    {
        var profileName = SelectedProfile?.Name ?? "none";
        var monitoring = _settings.MonitoringMode.ToString();
        var overlay = _overlayToggleCheckBox?.Checked == true ? "overlay on" : "overlay off";
        return $"Profile: {profileName} | Monitoring: {monitoring} | {overlay}";
    }

    private void SetTransientExperienceStatus(string message, bool warning = false, string? context = null, int durationMs = 4200)
    {
        _transientExperienceMessage = message;
        _transientExperienceContext = context;
        _transientExperienceWarning = warning;
        UpdateExperienceStatusFromState();

        _experienceStatusTimer.Stop();
        _experienceStatusTimer.Interval = Math.Max(1200, durationMs);
        _experienceStatusTimer.Start();
    }

    private void UpdateReadinessDiagnostics()
    {
        if (_diagnosticsSummaryLabel is null)
        {
            return;
        }

        var overlayArmed = _overlayToggleCheckBox?.Checked == true;
        var presentMonReady = _presentMonFpsService.IsBackendAvailable;
        var gpuReady = _systemTelemetryService.IsGpuTelemetryAvailable();
        var elevated = IsElevated();
        var session = _boostCoordinator.ActiveSession;
        var trayEnabled = _settings.MinimizeToTray;
        var handoffReady = session is null
            || session.RecoveryState.IsPreLaunchBoost
            || session.RecoveryState.AnchorProcessId <= 0
            || session.RecoveryState.GameProcessId > 0;

        SetDiagnosticLabel(
            _presentMonReadyLabel,
            "FPS backend",
            presentMonReady ? "Ready" : "Missing",
            presentMonReady
                ? Path.GetFileName(_presentMonFpsService.BackendPath) ?? "PresentMon bundle detected."
                : "PresentMon is not available in the release folder.",
            presentMonReady);

        SetDiagnosticLabel(
            _gpuReadyLabel,
            "GPU telemetry",
            gpuReady ? "Ready" : "Driver-limited",
            gpuReady
                ? "Windows GPU counters are available."
                : "GPU usage counters are unavailable on this driver or device.",
            gpuReady);

        SetDiagnosticLabel(
            _elevationReadyLabel,
            "Permissions",
            elevated ? "Elevated" : "Limited",
            elevated
                ? "CloudFrame can safely tune game and background process settings."
                : "Run elevated for the strongest tuning coverage.",
            elevated);

        SetDiagnosticLabel(
            _overlayReadyLabel,
            "Overlay",
            overlayArmed ? "Armed" : "Off",
            overlayArmed
                ? "Overlay will stay available for live session tracking."
                : "Enable the live overlay when you want in-game stats.",
            overlayArmed);

        var handoffDetail = session is not null
            && !session.RecoveryState.IsPreLaunchBoost
            && session.RecoveryState.AnchorProcessId > 0
            && session.RecoveryState.AnchorProcessId != session.RecoveryState.GameProcessId
                ? $"Launcher PID {session.RecoveryState.AnchorProcessId} handed off to live game PID {session.RecoveryState.GameProcessId}."
                : "Launcher-aware handoff is ready for wrapped launches like EA and anti-cheat starters.";
        SetDiagnosticLabel(
            _launcherHandoffLabel,
            "Launcher handoff",
            handoffReady ? "Ready" : "Pending",
            handoffDetail,
            handoffReady);

        SetDiagnosticLabel(
            _trayModeLabel,
            "Tray mode",
            trayEnabled ? "Enabled" : "Windowed",
            trayEnabled
                ? "Minimizing hides CloudFrame to the tray while the overlay keeps running."
                : "CloudFrame restores as a normal desktop window.",
            trayEnabled);

        var readyCount = 0;
        if (presentMonReady) readyCount++;
        if (gpuReady) readyCount++;
        if (elevated) readyCount++;
        if (overlayArmed) readyCount++;
        if (handoffReady) readyCount++;
        if (trayEnabled) readyCount++;

        _diagnosticsSummaryLabel.Text = readyCount >= 5
            ? "CloudFrame is in strong shape for live game testing. The capture path, boost permissions, and session routing are all ready."
            : "CloudFrame is usable, but this machine still has a few limits. The labels below show what is ready and what may reduce the full 1.1.0 experience.";
    }

    private static void SetDiagnosticLabel(Label label, string title, string state, string detail, bool healthy)
    {
        if (label is null)
        {
            return;
        }

        label.Text = $"{title}: {state} — {detail}";
        label.ForeColor = healthy ? AppTheme.Success : AppTheme.Warning;
    }

    private void UpdateFpsTrackingTarget()
    {
        if (!_startupReady)
        {
            return;
        }

        var session = _boostCoordinator.ActiveSession;
        if (session is not null && !session.RecoveryState.IsPreLaunchBoost)
        {
            ApplySessionFpsTarget(session);
            return;
        }

        if (_overlayToggleCheckBox?.Checked == true && TryApplyForegroundGameTarget())
        {
            return;
        }

        ClearFpsTarget();
    }

    private void ApplySessionFpsTarget(ActiveBoostSession session)
    {
        _presentMonFpsService.SetForegroundMode(false);
        _presentMonFpsService.SetTarget(
            session.RecoveryState.GameProcessId,
            session.Profile.Name,
            session.Profile.ExecutableName,
            session.AntiCheatStatus.UseCompatibilityMode,
            session.AntiCheatStatus.UseCompatibilityMode
                ? $"{session.AntiCheatStatus.DisplayName} compatibility mode keeps FPS capture disabled for safety."
                : null);
    }

    private bool TryApplyForegroundGameTarget()
    {
        var foregroundTarget = ResolveForegroundFpsTarget();
        if (foregroundTarget is null || !foregroundTarget.IsProfiledGame)
        {
            return false;
        }

        _presentMonFpsService.SetForegroundMode(false);
        _presentMonFpsService.SetTarget(
            foregroundTarget.ProcessId,
            foregroundTarget.DisplayName,
            foregroundTarget.MatchName,
            false,
            null);

        return true;
    }

    private void ClearFpsTarget()
    {
        _presentMonFpsService.SetForegroundMode(false);
        _presentMonFpsService.SetTarget(null, null, null, false, null);
    }

    private void UpdateShaderCacheLabel()
    {
        if (_shaderCacheLabel is null || _shaderCacheLabel.IsDisposed)
        {
            return;
        }

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
        OpenExternalPath(AppPaths.DataDirectory);
    }

    private void OpenLogFile()
    {
        AppPaths.EnsureDataDirectory();
        if (!File.Exists(AppPaths.LogPath))
        {
            File.WriteAllText(AppPaths.LogPath, string.Empty);
        }

        OpenExternalPath(AppPaths.LogPath);
    }

    private void OpenExternalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
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

    private async Task RefreshResolutionProcessesAsync()
    {
        if (_resolutionProcessComboBox is null || _resolutionWindowComboBox is null)
        {
            return;
        }

        SetResolutionStatus("Scanning visible windows...");
        IReadOnlyList<RunningProcessEntry> entries;
        try
        {
            entries = await Task.Run(() => _windowResolutionService.ListWindowedProcesses());
        }
        catch (Exception ex)
        {
            SetResolutionStatus($"Could not scan running windows: {ex.Message}", isError: true);
            return;
        }

        _resolutionProcessComboBox.BeginUpdate();
        try
        {
            _resolutionProcessComboBox.DataSource = null;
            _resolutionProcessComboBox.DisplayMember = nameof(RunningProcessEntry.ProcessName);
            _resolutionProcessComboBox.DataSource = entries.ToList();
        }
        finally
        {
            _resolutionProcessComboBox.EndUpdate();
        }

        if (_resolutionProcessComboBox.Items.Count > 0)
        {
            _resolutionProcessComboBox.SelectedIndex = 0;
            RefreshResolutionWindowChoices();
            SetResolutionStatus($"Found {_resolutionProcessComboBox.Items.Count} windowed app(s).");
        }
        else
        {
            _resolutionWindowComboBox.DataSource = null;
            SetResolutionStatus("No visible top-level windows were found. Launch a game or app first.", isError: true);
        }
    }

    private void RefreshResolutionWindowChoices()
    {
        if (_resolutionProcessComboBox?.SelectedItem is not RunningProcessEntry entry || _resolutionWindowComboBox is null)
        {
            return;
        }

        var windows = _windowResolutionService.ListWindowsForProcess(entry.ProcessId).ToList();
        _resolutionWindowComboBox.BeginUpdate();
        try
        {
            _resolutionWindowComboBox.DataSource = null;
            _resolutionWindowComboBox.DisplayMember = nameof(WindowTargetEntry.WindowTitle);
            _resolutionWindowComboBox.DataSource = windows;
        }
        finally
        {
            _resolutionWindowComboBox.EndUpdate();
        }

        if (_resolutionWindowComboBox.Items.Count > 0)
        {
            _resolutionWindowComboBox.SelectedIndex = 0;
            SyncResolutionSizeFromSelection();
        }
    }

    private void ApplyResolutionPresetSelection()
    {
        if (_resolutionPresetComboBox is null ||
            _resolutionWidthInput is null ||
            _resolutionHeightInput is null ||
            _resolutionPresetComboBox.SelectedItem is not string preset ||
            string.Equals(preset, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = preset.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var width) ||
            !int.TryParse(parts[1], out var height))
        {
            return;
        }

        _resolutionWidthInput.Value = Math.Clamp(width, (int)_resolutionWidthInput.Minimum, (int)_resolutionWidthInput.Maximum);
        _resolutionHeightInput.Value = Math.Clamp(height, (int)_resolutionHeightInput.Minimum, (int)_resolutionHeightInput.Maximum);
    }

    private void SyncResolutionSizeFromSelection()
    {
        if (_resolutionWindowComboBox?.SelectedItem is not WindowTargetEntry window ||
            _resolutionWidthInput is null ||
            _resolutionHeightInput is null)
        {
            return;
        }

        if (_windowResolutionService.TryGetWindowBounds(window.Handle, out var bounds, out _))
        {
            _resolutionWidthInput.Value = Math.Clamp(bounds.Width, (int)_resolutionWidthInput.Minimum, (int)_resolutionWidthInput.Maximum);
            _resolutionHeightInput.Value = Math.Clamp(bounds.Height, (int)_resolutionHeightInput.Minimum, (int)_resolutionHeightInput.Maximum);
            _resolutionPresetComboBox.SelectedItem = "Custom";
            SetResolutionStatus($"Loaded current size {bounds.Width} x {bounds.Height} for {window.ProcessName}.");
        }
    }

    private async Task ApplyResolutionLayoutAsync()
    {
        if (_resolutionWindowComboBox?.SelectedItem is not WindowTargetEntry window ||
            _resolutionWidthInput is null ||
            _resolutionHeightInput is null ||
            _resolutionBorderlessCheckBox is null)
        {
            MessageBox.Show(this, "Pick a running window first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            var width = (int)_resolutionWidthInput.Value;
            var height = (int)_resolutionHeightInput.Value;
            var borderless = _resolutionBorderlessCheckBox.Checked;

            var success = await Task.Run(() =>
                _windowResolutionService.TryApplyWindowLayout(window.Handle, width, height, borderless, out var error)
                    ? (Success: true, Error: string.Empty)
                    : (Success: false, Error: error ?? "CloudFrame could not change that window."));

            if (!success.Success)
            {
                SetResolutionStatus(success.Error, isError: true);
                MessageBox.Show(this, success.Error, "CloudFrame Resolution Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var borderText = borderless ? " with borderless enabled" : string.Empty;
            SetResolutionStatus($"Applied {width} x {height}{borderText} to {window.ProcessName}.");
            _logger.Log($"Applied resolution {width}x{height} to '{window.WindowTitle}' ({window.ProcessName}). Borderless: {borderless}.");
        }
        finally
        {
            Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void RestoreSelectedWindowBorder()
    {
        if (_resolutionWindowComboBox?.SelectedItem is not WindowTargetEntry window)
        {
            MessageBox.Show(this, "Pick a running window first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_windowResolutionService.TryRestoreWindowBorder(window.Handle, out var error))
        {
            SetResolutionStatus(error ?? "CloudFrame could not restore the window border.", isError: true);
            MessageBox.Show(this, error ?? "CloudFrame could not restore the window border.", "CloudFrame Resolution Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetResolutionStatus($"Restored the standard window frame for {window.ProcessName}.");
        _logger.Log($"Restored window border for '{window.WindowTitle}' ({window.ProcessName}).");
    }

    private void SetResolutionStatus(string message, bool isError = false)
    {
        if (_resolutionStatusLabel is null)
        {
            return;
        }

        _resolutionStatusLabel.Text = message;
        _resolutionStatusLabel.ForeColor = isError ? AppTheme.Warning : AppTheme.TextSecondary;
        SetTransientExperienceStatus(message, warning: isError, context: "Resolution Control");
    }

    private void UpdateFrameGenLabStatus()
    {
        if (_frameGenStatusLabel is null)
        {
            return;
        }

        var report = _frameGenPrototypeService.Evaluate(SelectedProfile, _boostCoordinator.ActiveSession, _settings);

        if (!_settings.EnableExperimentalFrameGen)
        {
            _frameGenStatusLabel.Text = report.IsBlocked
                ? $"Lab disabled. Current selection is blocked: {report.Summary}"
                : "Lab disabled. CloudFrame is staying on safe monitoring-only behavior, but the selected title is still being evaluated for a future external interpolation path.";
            _frameGenStatusLabel.ForeColor = AppTheme.TextSecondary;
        }
        else
        {
            var antiCheatGuard = _settings.FrameGenDisableOnAntiCheat
                ? "anti-cheat guard on"
                : "anti-cheat guard off";
            var latencyMode = _settings.FrameGenPreferLowLatency
                ? "latency-first"
                : "throughput-first";
            var borderMode = _settings.FrameGenRequireBorderless
                ? "borderless required"
                : "window mode flexible";

            _frameGenStatusLabel.Text = $"Experimental lab armed: {_settings.FrameGenCaptureMode} capture, {_settings.FrameGenBackend} backend, {borderMode}, {latencyMode}, {antiCheatGuard}. No synthetic frames are injected yet.";
            _frameGenStatusLabel.ForeColor = AppTheme.Warning;
        }

        if (_frameGenEligibilityLabel is null || _frameGenEligibilityDetailLabel is null)
        {
            return;
        }

        _frameGenEligibilityLabel.Text = report.Summary;
        _frameGenEligibilityLabel.ForeColor = report.IsBlocked
            ? AppTheme.Warning
            : report.CanPrepareLiveWindow
                ? AppTheme.Success
                : AppTheme.AccentStrong;
        _frameGenEligibilityDetailLabel.Text = report.Detail;
    }

    private async Task PrepareFrameGenPrototypeAsync()
    {
        if (!_settings.EnableExperimentalFrameGen)
        {
            MessageBox.Show(
                this,
                "Enable the Frame Gen Lab first if you want CloudFrame to prep a live prototype window.",
                "CloudFrame Frame Gen Lab",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var report = _frameGenPrototypeService.Evaluate(SelectedProfile, _boostCoordinator.ActiveSession, _settings);
        var result = await _frameGenPrototypeService.PrepareLiveWindowAsync(report, _settings);
        _logger.Log($"Frame Gen Lab prepare: {result.Message}");
        MessageBox.Show(
            this,
            result.Message,
            "CloudFrame Frame Gen Lab",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        SetTransientExperienceStatus(result.Message, warning: !result.Success, context: "Frame Gen Lab");
        UpdateFrameGenLabStatus();
    }

    private async Task LaunchFrameGenPrototypeAsync()
    {
        if (!_settings.EnableExperimentalFrameGen)
        {
            MessageBox.Show(
                this,
                "Enable the Frame Gen Lab first if you want CloudFrame to launch a prototype frame-generation session.",
                "CloudFrame Frame Gen Lab",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var report = _frameGenPrototypeService.Evaluate(SelectedProfile, _boostCoordinator.ActiveSession, _settings);
        var result = await _frameGenPrototypeService.LaunchPrototypeSessionAsync(report, _settings);
        _logger.Log($"Frame Gen Lab launch: {result.Message}");
        MessageBox.Show(
            this,
            result.Message,
            "CloudFrame Frame Gen Lab",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        SetTransientExperienceStatus(result.Message, warning: !result.Success, context: "Frame Gen Lab");
        UpdateFrameGenLabStatus();
    }

    private List<DetectedGame> FindMatchesForProfile(GameProfile profile)
    {
        var matches = new Dictionary<int, DetectedGame>();
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

                    var resolvedGameProcess = _processService.ResolveGameProcess(profile, process.Id);
                    var effectiveProcessId = resolvedGameProcess?.ProcessId ?? process.Id;
                    matches[effectiveProcessId] = new DetectedGame
                    {
                        Profile = profile,
                        ProcessId = effectiveProcessId,
                        ProcessName = resolvedGameProcess?.ProcessName ?? process.ProcessName,
                        ExecutablePath = resolvedGameProcess?.ExecutablePath ?? executablePath,
                        AnchorProcessId = process.Id,
                        AnchorProcessName = process.ProcessName
                    };
                }
                catch
                {
                }
            }
        }

        return matches.Values
            .OrderByDescending(static match => match.HasLauncherHandoff)
            .ThenBy(static match => match.ProcessName)
            .ToList();
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

                        var resolvedGameProcess = _processService.ResolveGameProcess(profile, process.Id);
                        return new DetectedGame
                        {
                            Profile = profile,
                            ProcessId = resolvedGameProcess?.ProcessId ?? process.Id,
                            ProcessName = resolvedGameProcess?.ProcessName ?? process.ProcessName,
                            ExecutablePath = resolvedGameProcess?.ExecutablePath ?? executablePath,
                            AnchorProcessId = process.Id,
                            AnchorProcessName = process.ProcessName
                        };
                    }

                    var fallbackResolvedGameProcess = _processService.ResolveGameProcess(profile, process.Id);
                    return new DetectedGame
                    {
                        Profile = profile,
                        ProcessId = fallbackResolvedGameProcess?.ProcessId ?? process.Id,
                        ProcessName = fallbackResolvedGameProcess?.ProcessName ?? process.ProcessName,
                        ExecutablePath = fallbackResolvedGameProcess?.ExecutablePath ?? profile.ExecutablePath,
                        AnchorProcessId = process.Id,
                        AnchorProcessName = process.ProcessName
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

    private static bool MatchesDashboardScanEntryFilter(DashboardScanEntry entry, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return entry.DisplayProfileName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.ProcessId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.ExecutablePath.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.WindowTitle.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleActiveSessionChanged(ActiveBoostSession? session)
    {
        if (_lastLiveSession is not null
            && !_lastLiveSession.RecoveryState.IsPreLaunchBoost
            && session is null)
        {
            CaptureLastSessionReport(_lastLiveSession);
        }

        _lastLiveSession = session is not null && !session.RecoveryState.IsPreLaunchBoost
            ? session
            : null;

        UpdateStatusLabels();
        RefreshDetectedGrid();

        UpdateFpsTrackingTarget();
        ScheduleTelemetryRefresh();
        ToggleOverlay(_overlayToggleCheckBox.Checked);
    }

    private void CaptureLastSessionReport(ActiveBoostSession session)
    {
        var delta = _fpsComparisonTracker.CompleteCurrentSession();
        var actionSummary = BuildAppliedActionSummary(session);
        var resultLabel = delta.HasResult
            ? $"{delta.Value} measured"
            : "No measured delta";
        var detail = delta.HasResult
            ? $"{delta.Detail} | Applied: {actionSummary}"
            : $"Applied: {actionSummary}. Keep the session running a little longer next time so CloudFrame can gather more comparison samples.";

        _lastSessionReport = new SessionReport
        {
            ProfileName = session.Profile.Name,
            Summary = resultLabel,
            Detail = detail,
            ResultLabel = delta.HasResult ? "Measured gain" : "Session summary",
            HasMeasuredGain = delta.HasResult,
            BaselineAverageFps = delta.BaselineMetrics?.AverageFps,
            LiveAverageFps = delta.LiveMetrics?.AverageFps,
            BaselineOnePercentLowFps = delta.BaselineMetrics?.OnePercentLowFps,
            LiveOnePercentLowFps = delta.LiveMetrics?.OnePercentLowFps,
            BaselinePacingScore = delta.BaselineMetrics?.PacingScore,
            LivePacingScore = delta.LiveMetrics?.PacingScore
        };

        SaveRecommendationMemory(session.Profile, _currentRecommendation);
        RequestProfileStudioRefresh();
    }

    private void ApplyCurrentRecommendation()
    {
        if (!_currentRecommendation.CanAutoApply)
        {
            _logger.Log("CloudFrame does not have a strong enough recommendation to auto-apply yet.");
            UpdateDashboardCards();
            return;
        }

        var profile = _boostCoordinator.ActiveSession?.Profile ?? SelectedProfile;
        if (profile is null)
        {
            _logger.Log("Select or boost a profile first so CloudFrame knows where to apply the recommendation.");
            return;
        }

        _profileTuningAdvisor.ApplyRecommendedTuning(profile, _currentRecommendation);
        SaveRecommendationMemory(profile, _currentRecommendation);
        SaveSettings();
        SyncBoostControlState();
        SyncPresetCardSelection();
        UpdateToolsSelectionInfo();
        RequestProfileStudioRefresh();
        UpdateDashboardCards();
        _logger.Log($"Applied tuning recommendation to '{profile.Name}'.");
    }

    private void SaveRecommendationMemory(GameProfile profile, ProfileTuningRecommendation recommendation)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(recommendation.Title))
        {
            return;
        }

        var existing = _settings.RecommendationMemories.FirstOrDefault(item => string.Equals(item.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new GameRecommendationMemory
            {
                ProfileId = profile.Id
            };
            _settings.RecommendationMemories.Add(existing);
        }

        existing.ProfileName = profile.Name;
        existing.RecommendedTitle = recommendation.Title;
        existing.RecommendedDetail = recommendation.Detail;
        existing.Action = recommendation.Action.ToStorageValue();
        existing.ExecutableName = profile.ExecutableName;
        existing.ExecutablePath = profile.ExecutablePath;
        existing.UpdatedAt = DateTimeOffset.Now;
        existing.CanAutoApply = recommendation.CanAutoApply;
        SaveSettings();
    }

    private ProfileTuningRecommendation? GetStoredRecommendation(GameProfile profile)
    {
        var memory = _settings.RecommendationMemories
            .Where(item =>
                string.Equals(item.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.ExecutablePath) &&
                 string.Equals(item.ExecutablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.ExecutableName) &&
                 string.Equals(item.ExecutableName, profile.ExecutableName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefault();

        if (memory is null)
        {
            return null;
        }

        var action = Enum.TryParse<ProfileTuningAction>(memory.Action, ignoreCase: true, out var parsedAction)
            ? parsedAction
            : ProfileTuningAction.HoldSteady;

        return new ProfileTuningRecommendation(
            $"Remembered: {memory.RecommendedTitle}",
            $"{memory.RecommendedDetail} Saved {memory.UpdatedAt.LocalDateTime:g}.",
            action,
            memory.CanAutoApply);
    }

    private GameRecommendationMemory? GetStoredRecommendationMemory(GameProfile profile)
    {
        return _settings.RecommendationMemories
            .Where(item =>
                string.Equals(item.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.ExecutablePath) &&
                 string.Equals(item.ExecutablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.ExecutableName) &&
                 string.Equals(item.ExecutableName, profile.ExecutableName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefault();
    }

    private int _statusLabelThrottleCounter;

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
            _telemetrySampleCounter++;
            var cpuTask = Task.Run(() => _systemTelemetryService.GetCpuPercent());
            Task<double?>? gpuTask = null;
            var shouldSampleGpu = _settings.MonitoringMode switch
            {
                MonitoringMode.Minimal => _telemetrySampleCounter % 3 == 0,
                MonitoringMode.Detailed => true,
                _ => _telemetrySampleCounter % 2 != 0
            };

            if (shouldSampleGpu)
            {
                gpuTask = Task.Run(() => _systemTelemetryService.GetGpuPercent());
                await Task.WhenAll(cpuTask, gpuTask).ConfigureAwait(false);
                _lastGpuTelemetry = gpuTask.Result;
            }
            else
            {
                await cpuTask.ConfigureAwait(false);
            }

            if (!hasLiveSession && _overlayToggleCheckBox?.Checked == true)
            {
                UpdateIdleTrackingTarget();
            }
            else if (hasLiveSession)
            {
                _lastKnownForegroundTarget = null;
                _fpsBecameNullAt = DateTimeOffset.MinValue;
            }

            var telemetry = new TelemetrySnapshot
            {
                CpuPercent = cpuTask.Result,
                GpuPercent = shouldSampleGpu ? _lastGpuTelemetry : _lastGpuTelemetry,
                FramesPerSecond = _presentMonFpsService.LatestFps,
                FrameTimeMs = _presentMonFpsService.LatestFrameTimeMs,
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

            _boostCoordinator.ObserveTelemetry(telemetry, _settings.EnableMaintenanceBackoff);

            if (InvokeRequired)
            {
                var baselineEligible = IsBaselineEligible(hasLiveSession);
                BeginInvoke(() => ApplyTelemetrySnapshot(telemetry, baselineEligible));
                return;
            }

            ApplyTelemetrySnapshot(telemetry, IsBaselineEligible(hasLiveSession));
        }
        finally
        {
            _telemetryRefreshGate.Release();
        }
    }

    private void UpdateIdleTrackingTarget()
    {
        var resolved = ResolveForegroundFpsTarget();
        if (resolved is not null && resolved.IsProfiledGame)
        {
            _lastKnownForegroundTarget = resolved;
        }
        else
        {
            _lastKnownForegroundTarget = null;
        }

        _fpsBecameNullAt = DateTimeOffset.MinValue;
    }

    private bool IsBaselineEligible(bool hasLiveSession)
    {
        return hasLiveSession || _lastKnownForegroundTarget?.IsProfiledGame == true;
    }

    private void HandlePresentMonSampleCaptured(FpsFrameSample sample)
    {
        var session = _boostCoordinator.ActiveSession;
        var baselineEligible = IsBaselineEligible(session is not null && !session.RecoveryState.IsPreLaunchBoost);
        _fpsComparisonTracker.ObserveFrameSample(sample, session, baselineEligible);
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
            var detectedMatch = _detectedGames.FirstOrDefault(game =>
                game.ProcessId == process.Id
                || game.AnchorProcessId == process.Id
                || (!string.IsNullOrWhiteSpace(game.ExecutablePath)
                    && string.Equals(game.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase))
                || string.Equals(game.ProcessName, process.ProcessName, StringComparison.OrdinalIgnoreCase));

            if (matchedProfile is null && detectedMatch is null)
            {
                return null;
            }

            return new FpsTrackingTarget
            {
                ProcessId = process.Id,
                ExecutablePath = executablePath,
                MatchName = matchedProfile?.ExecutableName ?? detectedMatch?.Profile.ExecutableName ?? process.ProcessName,
                DisplayName = matchedProfile?.Name
                    ?? detectedMatch?.Profile.Name
                    ?? (!string.IsNullOrWhiteSpace(title) ? title : process.ProcessName),
                IsProfiledGame = true
            };
        }
        catch
        {
            return null;
        }
    }

    private void ApplyTelemetrySnapshot(TelemetrySnapshot telemetry, bool baselineEligible)
    {
        _latestTelemetry = telemetry;
        _fpsComparisonTracker.Observe(telemetry, _boostCoordinator.ActiveSession, baselineEligible);
        _profileTuningAdvisor.Observe(telemetry, _boostCoordinator.ActiveSession);

        // Throttle the heavy label/card refresh — run every 5th tick (~7–8 seconds)
        // to avoid layout pressure and WinForms repaint thrash on every sample.
        _statusLabelThrottleCounter++;
        if (_statusLabelThrottleCounter % 5 == 0)
        {
            RequestProfileStudioRefresh();
        }

        if (ShouldDisplayOverlay(telemetry))
        {
            var overlay = EnsureOverlayForm();
            overlay.UpdateSnapshot(_latestTelemetry);
            if (!overlay.Visible)
            {
                overlay.Show();
            }
        }
        else
        {
            _overlayForm?.Hide();
        }

        RequestDashboardUiRefresh();
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
                overlay.Show();
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

    private void ApplyOverlayProfilePreset(OverlayProfilePreset preset)
    {
        if (preset == OverlayProfilePreset.Custom)
        {
            _settings.OverlayProfilePreset = preset;
            SaveSettings();
            return;
        }

        _settings.OverlayProfilePreset = preset;
        switch (preset)
        {
            case OverlayProfilePreset.Competitive:
                _settings.OverlayStyle = OverlayStyle.Minimal;
                _settings.OverlayFontPreset = OverlayFontPreset.Bahnschrift;
                _settings.OverlayShowFps = true;
                _settings.OverlayShowCpu = true;
                _settings.OverlayShowGpu = true;
                _settings.OverlayAccentArgb = unchecked((int)0xFFEC40C4);
                _settings.OverlayTextArgb = unchecked((int)0xFFF8FAFF);
                _settings.OverlayBackgroundArgb = unchecked((int)0x00000000);
                break;
            case OverlayProfilePreset.Minimal:
                _settings.OverlayStyle = OverlayStyle.Minimal;
                _settings.OverlayFontPreset = OverlayFontPreset.Consolas;
                _settings.OverlayShowFps = true;
                _settings.OverlayShowCpu = false;
                _settings.OverlayShowGpu = false;
                _settings.OverlayAccentArgb = unchecked((int)0xFF90F5FF);
                _settings.OverlayTextArgb = unchecked((int)0xFFF8FAFF);
                _settings.OverlayBackgroundArgb = unchecked((int)0x00000000);
                break;
            case OverlayProfilePreset.Streamer:
                _settings.OverlayStyle = OverlayStyle.Card;
                _settings.OverlayFontPreset = OverlayFontPreset.Trebuchet;
                _settings.OverlayShowFps = true;
                _settings.OverlayShowCpu = true;
                _settings.OverlayShowGpu = true;
                _settings.OverlayAccentArgb = unchecked((int)0xFF64D2FF);
                _settings.OverlayTextArgb = unchecked((int)0xFFF5FBFF);
                _settings.OverlayBackgroundArgb = unchecked((int)0xCC11161F);
                break;
            case OverlayProfilePreset.FullStats:
                _settings.OverlayStyle = OverlayStyle.Card;
                _settings.OverlayFontPreset = OverlayFontPreset.Segoe;
                _settings.OverlayShowFps = true;
                _settings.OverlayShowCpu = true;
                _settings.OverlayShowGpu = true;
                _settings.OverlayAccentArgb = unchecked((int)0xFF35D07F);
                _settings.OverlayTextArgb = unchecked((int)0xFFF7FBFF);
                _settings.OverlayBackgroundArgb = unchecked((int)0xD21A1F29);
                break;
        }

        SyncOverlayStudioControls();
        SaveSettings();
        RecreateOverlay();
        _logger.Log($"Applied overlay profile '{preset}'.");
    }

    private void SyncOverlayStudioControls()
    {
        if (_overlayProfileComboBox is null)
        {
            return;
        }

        _isSyncingOverlayStudio = true;
        try
        {
            _overlayProfileComboBox.SelectedItem = _settings.OverlayProfilePreset;
            _overlayStyleComboBox.SelectedIndex = _settings.OverlayStyle == OverlayStyle.Minimal ? 1 : 0;
            _overlayFontComboBox.SelectedItem = _settings.OverlayFontPreset;
            _overlayShowFpsCheckBox.Checked = _settings.OverlayShowFps;
            _overlayShowCpuCheckBox.Checked = _settings.OverlayShowCpu;
            _overlayShowGpuCheckBox.Checked = _settings.OverlayShowGpu;
            _overlayAccentPreview.BackColor = Color.FromArgb(255, Color.FromArgb(_settings.OverlayAccentArgb).R, Color.FromArgb(_settings.OverlayAccentArgb).G, Color.FromArgb(_settings.OverlayAccentArgb).B);
            _overlayTextPreview.BackColor = Color.FromArgb(255, Color.FromArgb(_settings.OverlayTextArgb).R, Color.FromArgb(_settings.OverlayTextArgb).G, Color.FromArgb(_settings.OverlayTextArgb).B);
            _overlayBackgroundPreview.BackColor = Color.FromArgb(255, Color.FromArgb(_settings.OverlayBackgroundArgb).R, Color.FromArgb(_settings.OverlayBackgroundArgb).G, Color.FromArgb(_settings.OverlayBackgroundArgb).B);
        }
        finally
        {
            _isSyncingOverlayStudio = false;
        }
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
                : _latestTelemetry.FpsStatus ?? "Waiting for FPS data";
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

        if (_performanceLabSummaryLabel is not null
            && _performanceLabBaselineLabel is not null
            && _performanceLabLiveLabel is not null
            && _performanceLabPacingLabel is not null)
        {
            var baselineMetrics = delta.BaselineMetrics;
            var liveMetrics = delta.LiveMetrics;

            if (session is not null && liveMetrics is not null)
            {
                _performanceLabSummaryLabel.Text = $"Live benchmark signal for {session.Profile.Name}: {liveMetrics.Value.AverageFps:0.#} FPS avg | {liveMetrics.Value.OnePercentLowFps:0.#} FPS 1% low";
                _performanceLabSummaryLabel.ForeColor = liveMetrics.Value.PacingScore >= 82
                    ? AppTheme.Success
                    : liveMetrics.Value.PacingScore >= 65
                        ? AppTheme.AccentSoft
                        : AppTheme.Warning;
            }
            else if (delta.HasResult && baselineMetrics is not null && liveMetrics is not null)
            {
                var avgSign = (delta.DeltaPercent ?? 0) >= 0 ? "+" : string.Empty;
                var lowSign = (delta.DeltaOnePercentLowFps ?? 0) >= 0 ? "+" : string.Empty;
                _performanceLabSummaryLabel.Text = $"Last measured session: {avgSign}{delta.DeltaPercent:0.#}% avg | {lowSign}{delta.DeltaOnePercentLowFps:0.#} FPS 1% low";
                _performanceLabSummaryLabel.ForeColor = (delta.DeltaPercent ?? 0) >= 0 || (delta.DeltaOnePercentLowFps ?? 0) > 0
                    ? AppTheme.Success
                    : AppTheme.Warning;
            }
            else
            {
                _performanceLabSummaryLabel.Text = "CloudFrame is calibrating benchmark-grade session metrics.";
                _performanceLabSummaryLabel.ForeColor = AppTheme.AccentSoft;
            }

            _performanceLabBaselineLabel.Text = baselineMetrics is null
                ? "Baseline: waiting for enough profiled-game idle samples."
                : $"Baseline: {FormatPerformanceMetrics(baselineMetrics.Value)}";
            _performanceLabLiveLabel.Text = liveMetrics is null
                ? "Live: waiting for enough boosted-session samples."
                : $"Live: {FormatPerformanceMetrics(liveMetrics.Value)}";
            _performanceLabPacingLabel.Text = liveMetrics is not null
                ? $"Session quality: {DescribePacing(liveMetrics.Value.PacingScore)} | Avg frame time {liveMetrics.Value.AverageFrameTimeMs:0.00} ms | P95 {liveMetrics.Value.P95FrameTimeMs:0.00} ms"
                : baselineMetrics is not null
                    ? $"Baseline quality: {DescribePacing(baselineMetrics.Value.PacingScore)} | Avg frame time {baselineMetrics.Value.AverageFrameTimeMs:0.00} ms | P95 {baselineMetrics.Value.P95FrameTimeMs:0.00} ms"
                    : "Pacing score estimates frametime consistency, not just raw FPS.";
        }

        if (_dashboardSummaryLabel is not null && _dashboardSummaryDetailLabel is not null)
        {
            var fpsPart = _latestTelemetry.FramesPerSecond is double currentFps
                ? $"{currentFps:0} FPS average"
                : "FPS warming up";
            var healthTone = session is not null
                ? session.AntiCheatStatus.UseCompatibilityMode
                    ? "System running safely in compatibility mode"
                    : "System running smoothly"
                : "System standing by";
            _dashboardSummaryLabel.Text = $"{healthTone} — {fpsPart}";
            _dashboardSummaryLabel.ForeColor = session?.AntiCheatStatus.UseCompatibilityMode == true
                ? AppTheme.Warning
                : _latestTelemetry.CpuPercent >= 90 || (_latestTelemetry.GpuPercent ?? 0) >= 97
                    ? AppTheme.Warning
                    : AppTheme.Success;
            _dashboardSummaryDetailLabel.Text = session is null
                ? "Choose a profile or use Optimize Automatically to let CloudFrame line up a safe gaming configuration."
                : $"Selected profile: {session.Profile.Name} | CPU {_latestTelemetry.CpuPercent:0}% | GPU {(_latestTelemetry.GpuPercent ?? 0):0}% | Overlay {(_overlayToggleCheckBox?.Checked == true ? "armed" : "hidden")}";
        }

        if (_advisorLabel is not null && _advisorDetailLabel is not null)
        {
            if (_currentRecommendation != ProfileTuningRecommendation.Empty && !string.IsNullOrWhiteSpace(_currentRecommendation.Title))
            {
                _advisorLabel.Text = _currentRecommendation.Title;
                _advisorLabel.ForeColor = _currentRecommendation.CanAutoApply ? AppTheme.Success : AppTheme.AccentSoft;
                _advisorDetailLabel.Text = _currentRecommendation.Detail;
            }
            else if (_latestTelemetry.CpuPercent >= 92)
            {
                _advisorLabel.Text = "CPU pressure is the main limiter right now";
                _advisorLabel.ForeColor = AppTheme.Warning;
                _advisorDetailLabel.Text = "CloudFrame is seeing heavy CPU load. Try a stronger preset, close background browser tabs, or keep the live dashboard in Simplified mode while gaming.";
            }
            else if ((_latestTelemetry.GpuPercent ?? 0) >= 96)
            {
                _advisorLabel.Text = "This session looks GPU-bound";
                _advisorLabel.ForeColor = AppTheme.AccentSoft;
                _advisorDetailLabel.Text = "CloudFrame can still help with frametime stability, but the next meaningful gain is more likely to come from game settings, upscaling, or driver-level tuning.";
            }
            else
            {
                _advisorLabel.Text = "CloudFrame is ready to suggest next steps";
                _advisorLabel.ForeColor = AppTheme.AccentSoft;
                _advisorDetailLabel.Text = "Run a short boosted session and CloudFrame will surface recommended tuning based on real CPU, GPU, and FPS behavior.";
            }
        }

        if (_boostGauge is not null)
        {
            _boostGauge.Value = Math.Clamp(impactScore, 0, 100);
            _boostGauge.Caption = session is null ? "Boost scope" : "Applied actions";
            _boostGauge.Detail = session is null
                ? BuildPlannedActionSummary(impactSource)
                : BuildAppliedActionSummary(session);
        }

        if (_sessionReportLabel is not null && _sessionReportDetailLabel is not null)
        {
            if (session is not null && !session.RecoveryState.IsPreLaunchBoost)
            {
                _sessionReportLabel.Text = $"Live session: {session.Profile.Name}";
                _sessionReportLabel.ForeColor = AppTheme.AccentStrong;
                _sessionReportDetailLabel.Text = "CloudFrame is collecting post-session comparison data now. Restore when you are done to lock in the measured result summary.";
            }
            else if (_lastSessionReport is not null)
            {
                _sessionReportLabel.Text = $"{_lastSessionReport.ProfileName} — {_lastSessionReport.Summary}";
                _sessionReportLabel.ForeColor = _lastSessionReport.HasMeasuredGain ? AppTheme.Success : AppTheme.Warning;
                _sessionReportDetailLabel.Text = _lastSessionReport.Detail;
            }
            else
            {
                _sessionReportLabel.Text = "No completed boost session yet.";
                _sessionReportLabel.ForeColor = AppTheme.AccentSoft;
                _sessionReportDetailLabel.Text = "Boost a selected game, play for a bit, then restore to see a measured result summary here.";
            }
        }

        if (_recommendationLabel is not null && _recommendationDetailLabel is not null)
        {
            var liveRecommendation = _profileTuningAdvisor.GetRecommendation(impactSource, session, delta);
            var storedRecommendation = session is null && SelectedProfile is not null
                ? GetStoredRecommendation(SelectedProfile)
                : null;

            _currentRecommendation = storedRecommendation ?? liveRecommendation;

            _recommendationLabel.Text = _currentRecommendation.Title;
            _recommendationLabel.ForeColor = _currentRecommendation.CanAutoApply ? AppTheme.Success : AppTheme.AccentSoft;
            _recommendationDetailLabel.Text = _currentRecommendation.Detail;
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

    private static string FormatPerformanceMetrics(FramePerformanceMetrics metrics)
    {
        return $"{metrics.AverageFps:0.#} FPS avg | {metrics.OnePercentLowFps:0.#} FPS 1% low | pacing {metrics.PacingScore:0}";
    }

    private static string DescribePacing(double pacingScore)
    {
        if (pacingScore >= 88)
        {
            return "very smooth pacing";
        }

        if (pacingScore >= 74)
        {
            return "healthy pacing";
        }

        if (pacingScore >= 58)
        {
            return "mixed pacing";
        }

        return "unstable pacing";
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private sealed class FpsTrackingTarget
    {
        public required int ProcessId { get; init; }

        public required string ExecutablePath { get; init; }

        public required string MatchName { get; init; }

        public required string DisplayName { get; init; }

        public bool IsProfiledGame { get; init; }
    }

}
