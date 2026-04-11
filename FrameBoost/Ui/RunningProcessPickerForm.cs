using FrameBoost.Models;

namespace FrameBoost.Ui;

internal sealed class RunningProcessPickerForm : Form
{
    private readonly DataGridView _grid;
    private readonly TextBox _filterTextBox;
    private readonly List<RunningProcessEntry> _allEntries;

    public RunningProcessEntry? SelectedEntry { get; private set; }

    public RunningProcessPickerForm(IReadOnlyList<RunningProcessEntry> entries)
    {
        _allEntries = entries.OrderBy(static item => item.ProcessName).ToList();

        Text = "Choose Running Game Process";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 540);
        AutoScaleMode = AutoScaleMode.Dpi;
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(
            Math.Max(MinimumSize.Width, Math.Min(workingArea.Width - 96, 1180)),
            Math.Max(MinimumSize.Height, Math.Min(workingArea.Height - 96, 760)));
        BackColor = AppTheme.Canvas;
        Font = AppTheme.BodyFont();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
            BackColor = AppTheme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            Font = AppTheme.BodyFont(10f),
            ForeColor = AppTheme.TextSecondary,
            Text = "Pick the actual running game process. This is useful when launchers or anti-cheat loaders start a different game executable."
        };

        _filterTextBox = AppTheme.StyleTextBox(new TextBox { Dock = DockStyle.Top });
        _filterTextBox.PlaceholderText = "Filter by process, window title, or path";
        _filterTextBox.Margin = new Padding(0, 10, 0, 10);
        _filterTextBox.TextChanged += (_, _) => ReloadGrid();

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.Canvas,
            AutoSize = true
        };
        topPanel.Controls.Add(intro, 0, 0);
        topPanel.Controls.Add(_filterTextBox, 0, 1);

        _grid = new DataGridView
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
        AppTheme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "Process", Width = 170 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Window", HeaderText = "Window Title", Width = 280 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Executable", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.CellDoubleClick += (_, _) => ConfirmSelection();

        var buttonHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas
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
        var selectButton = AppTheme.CreateButton("Use Selected", primary: true, width: 132);
        selectButton.Click += (_, _) => ConfirmSelection();
        var cancelButton = AppTheme.CreateButton("Cancel", width: 100);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(selectButton);
        buttonHost.Controls.Add(buttons);

        AcceptButton = selectButton;
        CancelButton = cancelButton;

        root.Controls.Add(topPanel, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(buttonHost, 0, 2);

        Controls.Add(root);
        ReloadGrid();
    }

    private void ReloadGrid()
    {
        _grid.Rows.Clear();
        var filter = _filterTextBox.Text.Trim();

        foreach (var entry in _allEntries.Where(item => MatchesFilter(item, filter)))
        {
            var rowIndex = _grid.Rows.Add(entry.ProcessName, entry.ProcessId, entry.WindowTitle, entry.ExecutablePath);
            _grid.Rows[rowIndex].Tag = entry;
        }
    }

    private void ConfirmSelection()
    {
        if (_grid.CurrentRow?.Tag is not RunningProcessEntry entry)
        {
            MessageBox.Show(this, "Select a running process first.", "CloudFrame", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedEntry = entry;
        DialogResult = DialogResult.OK;
    }

    private static bool MatchesFilter(RunningProcessEntry entry, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return entry.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.WindowTitle.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.ExecutablePath.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
