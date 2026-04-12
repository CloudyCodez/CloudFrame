using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class FirstRunSetupForm : Form
{
    private readonly Func<List<RunningProcessEntry>> _runningProcessProvider;
    private readonly CheckedListBox _appChecklist;
    private readonly TextBox _gameOneNameTextBox;
    private readonly TextBox _gameOnePathTextBox;
    private readonly TextBox _gameTwoNameTextBox;
    private readonly TextBox _gameTwoPathTextBox;
    private readonly TextBox _gameThreeNameTextBox;
    private readonly TextBox _gameThreePathTextBox;
    private readonly ComboBox _presetComboBox;
    private readonly CheckBox _trayCheckBox;
    private readonly CheckBox _overlayCheckBox;

    public List<GameProfile> ProfilesToCreate { get; } = [];

    public bool EnableTrayMode => _trayCheckBox.Checked;

    public OverlayProfilePreset OverlayPreset => _overlayCheckBox.Checked
        ? OverlayProfilePreset.Competitive
        : OverlayProfilePreset.Minimal;

    public FirstRunSetupForm(Func<List<RunningProcessEntry>> runningProcessProvider)
    {
        _runningProcessProvider = runningProcessProvider;

        Text = "Welcome to CloudFrame";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 760);
        Size = new Size(1000, 840);
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
            Text = "Let’s get your first gaming profiles ready",
            AutoSize = true,
            Font = AppTheme.TitleFont(18f),
            ForeColor = AppTheme.TextPrimary
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "Pick up to three games you actually play, choose a safe starter preset, and select which background app groups CloudFrame should manage for you. You can still fine-tune everything later.",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 0, 14)
        }, 0, 1);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var profileCard = CreateSectionCard("Starter profiles", "Use real game executables where possible. If a launcher is open already, the process picker can bind it quickly.");
        var profileLayout = (TableLayoutPanel)profileCard.Controls[0];

        _gameOneNameTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });
        _gameOnePathTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });
        _gameTwoNameTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });
        _gameTwoPathTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });
        _gameThreeNameTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });
        _gameThreePathTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill });

        profileLayout.Controls.Add(CreatePathRow("Game 1", _gameOneNameTextBox, _gameOnePathTextBox), 0, 2);
        profileLayout.Controls.Add(CreatePathRow("Game 2", _gameTwoNameTextBox, _gameTwoPathTextBox), 0, 3);
        profileLayout.Controls.Add(CreatePathRow("Game 3", _gameThreeNameTextBox, _gameThreePathTextBox), 0, 4);

        var behaviorCard = CreateSectionCard("Starter behavior", "These settings become your default starting point. CloudFrame will still recommend smarter tuning later based on live sessions.");
        var behaviorLayout = (TableLayoutPanel)behaviorCard.Controls[0];
        _presetComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200
        });
        foreach (var preset in Enum.GetValues<BoostPresetOption>())
        {
            _presetComboBox.Items.Add(preset);
        }
        _presetComboBox.SelectedItem = BoostPresetOption.Performance;
        _trayCheckBox = new CheckBox
        {
            Text = "Minimize CloudFrame to tray",
            AutoSize = true,
            Checked = true,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        };
        _overlayCheckBox = new CheckBox
        {
            Text = "Use the Competitive overlay style",
            AutoSize = true,
            Checked = true,
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        };
        var presetFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = AppTheme.Surface
        };
        presetFlow.Controls.Add(new Label
        {
            Text = "Default preset:",
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CaptionFont(9.5f),
            Padding = new Padding(0, 8, 8, 0),
            BackColor = AppTheme.Surface
        });
        presetFlow.Controls.Add(_presetComboBox);
        presetFlow.Controls.Add(_trayCheckBox);
        presetFlow.Controls.Add(_overlayCheckBox);
        behaviorLayout.Controls.Add(presetFlow, 0, 2);

        var appsCard = CreateSectionCard("Safe target templates", "These are the background app groups CloudFrame will start with. You can add or remove anything later per profile.");
        var appsLayout = (TableLayoutPanel)appsCard.Controls[0];
        _appChecklist = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            Height = 180,
            CheckOnClick = true,
            BorderStyle = BorderStyle.None,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.BodyFont(9.5f)
        };
        foreach (var template in SafeTargetTemplate.CreateDefaults())
        {
            _appChecklist.Items.Add(template, template.IsRecommended);
        }
        appsLayout.Controls.Add(_appChecklist, 0, 2);

        content.Controls.Add(profileCard, 0, 0);
        content.Controls.Add(behaviorCard, 0, 1);
        content.Controls.Add(appsCard, 0, 2);
        root.Controls.Add(content, 0, 2);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.Canvas
        };
        var finishButton = AppTheme.CreateButton("Create Starter Setup", primary: true, width: 188);
        finishButton.Click += (_, _) => SaveAndClose();
        var skipButton = AppTheme.CreateButton("Skip for Now", width: 120);
        skipButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttonFlow.Controls.Add(finishButton);
        buttonFlow.Controls.Add(skipButton);
        root.Controls.Add(buttonFlow, 0, 3);

        AcceptButton = finishButton;
        CancelButton = skipButton;
        Controls.Add(root);
    }

    private CardPanel CreateSectionCard(string title, string subtitle)
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
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface
        };
        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.TitleFont(14f),
            ForeColor = AppTheme.TextPrimary,
            BackColor = AppTheme.Surface
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            Font = AppTheme.BodyFont(9.5f),
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreatePathRow(string label, TextBox nameBox, TextBox pathBox)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 0, 0, 10)
        };
        row.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CaptionFont(9.5f),
            BackColor = AppTheme.Surface
        }, 0, 0);
        row.Controls.Add(nameBox, 0, 1);

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0)
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var browseButton = AppTheme.CreateButton("Browse...", width: 108);
        browseButton.Click += (_, _) => BrowseForExecutable(nameBox, pathBox);
        var processButton = AppTheme.CreateButton("Use Running Process", width: 168);
        processButton.Click += (_, _) => UseRunningProcess(nameBox, pathBox);
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);
        pathRow.Controls.Add(processButton, 2, 0);
        row.Controls.Add(pathRow, 0, 2);
        return row;
    }

    private void BrowseForExecutable(TextBox nameBox, TextBox pathBox)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Windows executables (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Choose a game executable"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        pathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            nameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void UseRunningProcess(TextBox nameBox, TextBox pathBox)
    {
        var entries = _runningProcessProvider();
        if (entries.Count == 0)
        {
            MessageBox.Show(this, "No visible running processes were found. Launch the game first, then try again.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var picker = new RunningProcessPickerForm(entries);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedEntry is null)
        {
            return;
        }

        pathBox.Text = picker.SelectedEntry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            nameBox.Text = Path.GetFileNameWithoutExtension(picker.SelectedEntry.ExecutablePath);
        }
    }

    private void SaveAndClose()
    {
        ProfilesToCreate.Clear();

        var selectedProcesses = _appChecklist.CheckedItems
            .OfType<SafeTargetTemplate>()
            .SelectMany(static template => template.Processes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var preset = _presetComboBox.SelectedItem is BoostPresetOption selectedPreset
            ? selectedPreset
            : BoostPresetOption.Performance;

        AddProfileIfValid(_gameOneNameTextBox.Text, _gameOnePathTextBox.Text, preset, selectedProcesses);
        AddProfileIfValid(_gameTwoNameTextBox.Text, _gameTwoPathTextBox.Text, preset, selectedProcesses);
        AddProfileIfValid(_gameThreeNameTextBox.Text, _gameThreePathTextBox.Text, preset, selectedProcesses);

        if (ProfilesToCreate.Count == 0)
        {
            MessageBox.Show(this, "Add at least one valid game executable to build a starter setup.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private void AddProfileIfValid(string name, string path, BoostPresetOption preset, IReadOnlyCollection<string> processes)
    {
        var trimmedName = name.Trim();
        var trimmedPath = path.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || string.IsNullOrWhiteSpace(trimmedPath) || !File.Exists(trimmedPath))
        {
            return;
        }

        ProfilesToCreate.Add(new GameProfile
        {
            Name = trimmedName,
            ExecutablePath = trimmedPath,
            AutoBoost = true,
            SwitchPowerPlan = true,
            BoostPreset = preset,
            BoostGamePriority = true,
            LowerBackgroundProcesses = true,
            TrimBackgroundMemory = true,
            UseBackgroundMemoryPriority = true,
            UseBackgroundEcoQos = preset is BoostPresetOption.Performance or BoostPresetOption.MaxFps,
            EnableRecurringMaintenance = preset is not BoostPresetOption.Balanced,
            BackgroundProcessesRaw = string.Join(", ", processes)
        });
    }

    private sealed class SafeTargetTemplate
    {
        public string Name { get; init; } = string.Empty;

        public bool IsRecommended { get; init; }

        public IReadOnlyList<string> Processes { get; init; } = [];

        public override string ToString() => Name;

        public static IReadOnlyList<SafeTargetTemplate> CreateDefaults()
        {
            return
            [
                new SafeTargetTemplate { Name = "Launchers", IsRecommended = true, Processes = ["SteamWebHelper", "EpicGamesLauncher", "GalaxyClient", "RiotClientServices", "EADesktop", "UbisoftConnect", "Battle.net"] },
                new SafeTargetTemplate { Name = "Browsers", IsRecommended = true, Processes = ["Chrome", "msedge", "firefox", "opera", "brave"] },
                new SafeTargetTemplate { Name = "Chat and Voice", IsRecommended = true, Processes = ["Discord", "Slack", "Telegram", "Teams"] },
                new SafeTargetTemplate { Name = "Overlays and Capture", IsRecommended = false, Processes = ["Overwolf", "Discord", "SteamWebHelper", "NVIDIA Share", "RadeonSoftware"] },
                new SafeTargetTemplate { Name = "Sync Apps", IsRecommended = false, Processes = ["OneDrive", "Dropbox", "GoogleDriveFS"] }
            ];
        }
    }
}
