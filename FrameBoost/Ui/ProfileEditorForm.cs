using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class ProfileEditorForm : Form
{
    private readonly Func<List<RunningProcessEntry>> _runningProcessProvider;
    private readonly IReadOnlyList<PowerPlanInfo> _powerPlans;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _pathTextBox;
    private readonly CheckBox _autoBoostCheckBox;
    private readonly CheckBox _switchPowerPlanCheckBox;
    private readonly CheckBox _boostGamePriorityCheckBox;
    private readonly ComboBox _powerPlanComboBox;
    private readonly ComboBox _priorityComboBox;
    private readonly ComboBox _presetComboBox;
    private readonly CheckBox _lowerBackgroundCheckBox;
    private readonly CheckBox _trimBackgroundMemoryCheckBox;
    private readonly CheckBox _closeBackgroundAppsCheckBox;
    private readonly CheckBox _useMemoryPriorityCheckBox;
    private readonly CheckBox _useEcoQosCheckBox;
    private readonly CheckBox _enableRecurringMaintenanceCheckBox;
    private readonly TextBox _backgroundProcessesTextBox;

    public GameProfile WorkingCopy { get; }

    public ProfileEditorForm(GameProfile profile, IReadOnlyList<PowerPlanInfo> powerPlans, Func<List<RunningProcessEntry>> runningProcessProvider)
    {
        WorkingCopy = profile;
        _powerPlans = powerPlans;
        _runningProcessProvider = runningProcessProvider;

        Text = $"Edit Profile - {profile.Name}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 640);
        BackColor = AppTheme.Canvas;
        Font = AppTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(
            Math.Max(MinimumSize.Width, Math.Min(workingArea.Width - 96, 1080)),
            Math.Max(MinimumSize.Height, Math.Min(workingArea.Height - 96, 760)));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Text = "Set the real game executable here. If a launcher, anti-cheat loader, or bootstrapper starts a different process, use the running-process picker while the game is open.",
            Margin = new Padding(0, 0, 0, 6)
        };

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 15,
            Padding = new Padding(0, 14, 0, 14),
            AutoScroll = true,
            BackColor = AppTheme.Canvas
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 14; row++)
        {
            editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nameTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill, Text = profile.Name });
        _pathTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Fill, Text = profile.ExecutablePath });
        _autoBoostCheckBox = new CheckBox
        {
            Text = "Include this profile when CloudFrame scans running processes",
            AutoSize = true,
            Checked = profile.AutoBoost
        };
        _switchPowerPlanCheckBox = new CheckBox
        {
            Text = "Switch to the selected gaming power plan while boosting",
            AutoSize = true,
            Checked = profile.SwitchPowerPlan
        };
        _boostGamePriorityCheckBox = new CheckBox
        {
            Text = "Raise the game process priority during boost",
            AutoSize = true,
            Checked = profile.BoostGamePriority
        };
        _powerPlanComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        });
        _priorityComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        });
        _presetComboBox = AppTheme.StyleComboBox(new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        });
        _lowerBackgroundCheckBox = new CheckBox
        {
            Text = "Lower selected background apps to BelowNormal while gaming",
            AutoSize = true,
            Checked = profile.LowerBackgroundProcesses
        };
        _trimBackgroundMemoryCheckBox = new CheckBox
        {
            Text = "Trim listed background app memory working sets during boost",
            AutoSize = true,
            Checked = profile.TrimBackgroundMemory
        };
        _closeBackgroundAppsCheckBox = new CheckBox
        {
            Text = "Gracefully close listed background apps when the preset is aggressive",
            AutoSize = true,
            Checked = profile.CloseBackgroundAppsGracefully
        };
        _useMemoryPriorityCheckBox = new CheckBox
        {
            Text = "Lower listed background app memory priority while the game is boosted",
            AutoSize = true,
            Checked = profile.UseBackgroundMemoryPriority
        };
        _useEcoQosCheckBox = new CheckBox
        {
            Text = "Apply EcoQoS to listed background apps on stronger presets",
            AutoSize = true,
            Checked = profile.UseBackgroundEcoQos
        };
        _enableRecurringMaintenanceCheckBox = new CheckBox
        {
            Text = "Keep reapplying cleanup during the session instead of only once at launch",
            AutoSize = true,
            Checked = profile.EnableRecurringMaintenance
        };
        _backgroundProcessesTextBox = AppTheme.StyleTextBox(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 120,
            ScrollBars = ScrollBars.Vertical,
            Text = profile.BackgroundProcessesRaw
        }, multiline: true);

        BindPriorityOptions(profile.GamePriority);
        BindPresetOptions(profile.BoostPreset);
        BindPowerPlans(profile.PreferredPowerPlanGuid);

        var browseButton = AppTheme.CreateButton("Browse...", width: 108);
        browseButton.Margin = new Padding(10, 0, 0, 0);
        browseButton.Click += (_, _) => BrowseForExecutable();

        var runningProcessButton = AppTheme.CreateButton("Use Running Process", width: 168);
        runningProcessButton.Margin = new Padding(10, 0, 0, 0);
        runningProcessButton.Click += (_, _) => UseRunningProcess();

        var pathButtonRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0)
        };
        pathButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathButtonRow.Controls.Add(_pathTextBox, 0, 0);
        pathButtonRow.Controls.Add(browseButton, 1, 0);
        pathButtonRow.Controls.Add(runningProcessButton, 2, 0);

        editor.Controls.Add(CreateFieldLabel("Name"), 0, 0);
        editor.Controls.Add(_nameTextBox, 1, 0);
        editor.Controls.Add(CreateFieldLabel("Executable"), 0, 1);
        editor.Controls.Add(pathButtonRow, 1, 1);
        editor.Controls.Add(CreateFieldLabel("Scan Inclusion"), 0, 2);
        editor.Controls.Add(_autoBoostCheckBox, 1, 2);
        editor.Controls.Add(CreateFieldLabel("Power Plan"), 0, 3);
        editor.Controls.Add(_switchPowerPlanCheckBox, 1, 3);
        editor.Controls.Add(CreateFieldLabel("Power Target"), 0, 4);
        editor.Controls.Add(_powerPlanComboBox, 1, 4);
        editor.Controls.Add(CreateFieldLabel("Game Priority"), 0, 5);
        editor.Controls.Add(_boostGamePriorityCheckBox, 1, 5);
        editor.Controls.Add(CreateFieldLabel("Priority Target"), 0, 6);
        editor.Controls.Add(_priorityComboBox, 1, 6);
        editor.Controls.Add(CreateFieldLabel("Boost Preset"), 0, 7);
        editor.Controls.Add(_presetComboBox, 1, 7);
        editor.Controls.Add(CreateFieldLabel("Background Apps"), 0, 8);
        editor.Controls.Add(_lowerBackgroundCheckBox, 1, 8);
        editor.Controls.Add(CreateFieldLabel("Memory Trim"), 0, 9);
        editor.Controls.Add(_trimBackgroundMemoryCheckBox, 1, 9);
        editor.Controls.Add(CreateFieldLabel("Graceful Close"), 0, 10);
        editor.Controls.Add(_closeBackgroundAppsCheckBox, 1, 10);
        editor.Controls.Add(CreateFieldLabel("Memory Priority"), 0, 11);
        editor.Controls.Add(_useMemoryPriorityCheckBox, 1, 11);
        editor.Controls.Add(CreateFieldLabel("EcoQoS"), 0, 12);
        editor.Controls.Add(_useEcoQosCheckBox, 1, 12);
        editor.Controls.Add(CreateFieldLabel("Maintenance"), 0, 13);
        editor.Controls.Add(_enableRecurringMaintenanceCheckBox, 1, 13);
        editor.Controls.Add(CreateFieldLabel("Process Names"), 0, 14);
        editor.Controls.Add(_backgroundProcessesTextBox, 1, 14);

        var buttonHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(0, 8, 0, 0)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.Canvas
        };
        var saveButton = AppTheme.CreateButton("Save Profile", primary: true, width: 132);
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = AppTheme.CreateButton("Cancel", width: 100);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);
        buttonHost.Controls.Add(buttons);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(editor, 0, 1);
        root.Controls.Add(buttonHost, 0, 2);
        Controls.Add(root);
    }

    private void BrowseForExecutable()
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

        _pathTextBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text) || string.Equals(_nameTextBox.Text, "New Game Profile", StringComparison.OrdinalIgnoreCase))
        {
            _nameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void UseRunningProcess()
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

        _pathTextBox.Text = picker.SelectedEntry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text) || string.Equals(_nameTextBox.Text, "New Game Profile", StringComparison.OrdinalIgnoreCase))
        {
            _nameTextBox.Text = Path.GetFileNameWithoutExtension(picker.SelectedEntry.ExecutablePath);
        }
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, "Enter a profile name first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_pathTextBox.Text) || !File.Exists(_pathTextBox.Text))
        {
            MessageBox.Show(this, "Choose a valid game executable path.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        WorkingCopy.Name = _nameTextBox.Text.Trim();
        WorkingCopy.ExecutablePath = _pathTextBox.Text.Trim();
        WorkingCopy.AutoBoost = _autoBoostCheckBox.Checked;
        WorkingCopy.SwitchPowerPlan = _switchPowerPlanCheckBox.Checked;
        WorkingCopy.BoostGamePriority = _boostGamePriorityCheckBox.Checked;
        WorkingCopy.GamePriority = _priorityComboBox.SelectedItem is ProcessPriorityOption selectedPriority
            ? selectedPriority
            : ProcessPriorityOption.High;
        WorkingCopy.BoostPreset = _presetComboBox.SelectedItem is BoostPresetOption selectedPreset
            ? selectedPreset
            : BoostPresetOption.Performance;
        WorkingCopy.LowerBackgroundProcesses = _lowerBackgroundCheckBox.Checked;
        WorkingCopy.TrimBackgroundMemory = _trimBackgroundMemoryCheckBox.Checked;
        WorkingCopy.CloseBackgroundAppsGracefully = _closeBackgroundAppsCheckBox.Checked;
        WorkingCopy.UseBackgroundMemoryPriority = _useMemoryPriorityCheckBox.Checked;
        WorkingCopy.UseBackgroundEcoQos = _useEcoQosCheckBox.Checked;
        WorkingCopy.EnableRecurringMaintenance = _enableRecurringMaintenanceCheckBox.Checked;
        WorkingCopy.BackgroundProcessesRaw = _backgroundProcessesTextBox.Text;

        var selectedPlan = _powerPlanComboBox.SelectedItem as PowerPlanChoice;
        WorkingCopy.PreferredPowerPlanGuid = selectedPlan?.Guid;
        WorkingCopy.PreferredPowerPlanName = _powerPlans.FirstOrDefault(plan => plan.Guid == selectedPlan?.Guid)?.Name;

        DialogResult = DialogResult.OK;
    }

    private void BindPriorityOptions(ProcessPriorityOption selectedPriority)
    {
        _priorityComboBox.Items.Clear();

        foreach (var item in Enum.GetValues<ProcessPriorityOption>())
        {
            _priorityComboBox.Items.Add(item);
        }

        if (_priorityComboBox.Items.Count == 0)
        {
            return;
        }

        var matchingIndex = _priorityComboBox.Items.IndexOf(selectedPriority);
        if (matchingIndex >= 0)
        {
            _priorityComboBox.SelectedItem = selectedPriority;
            return;
        }

        _priorityComboBox.SelectedIndex = 0;
    }

    private void BindPresetOptions(BoostPresetOption selectedPreset)
    {
        _presetComboBox.Items.Clear();

        foreach (var item in Enum.GetValues<BoostPresetOption>())
        {
            _presetComboBox.Items.Add(item);
        }

        if (_presetComboBox.Items.Count == 0)
        {
            return;
        }

        var matchingIndex = _presetComboBox.Items.IndexOf(selectedPreset);
        if (matchingIndex >= 0)
        {
            _presetComboBox.SelectedItem = selectedPreset;
            return;
        }

        _presetComboBox.SelectedIndex = 0;
    }

    private void BindPowerPlans(string? selectedGuid)
    {
        var items = new List<PowerPlanChoice>
        {
            new() { DisplayName = "Keep current plan", Guid = null }
        };
        items.AddRange(_powerPlans.Select(static plan => new PowerPlanChoice
        {
            DisplayName = plan.IsActive ? $"{plan.Name} (Active)" : plan.Name,
            Guid = plan.Guid
        }));

        _powerPlanComboBox.Items.Clear();

        foreach (var item in items)
        {
            _powerPlanComboBox.Items.Add(item);
        }

        if (_powerPlanComboBox.Items.Count == 0)
        {
            return;
        }

        var matchingIndex = items.FindIndex(item => string.Equals(item.Guid, selectedGuid, StringComparison.OrdinalIgnoreCase));
        _powerPlanComboBox.SelectedIndex = matchingIndex >= 0 ? matchingIndex : 0;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(0, 8, 12, 8),
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CaptionFont(9.5f)
        };
    }

    private sealed class PowerPlanChoice
    {
        public string DisplayName { get; init; } = string.Empty;

        public string? Guid { get; init; }

        public override string ToString() => DisplayName;
    }
}
