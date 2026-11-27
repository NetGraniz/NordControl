namespace NordControlUi;

internal sealed class SettingsForm : Form
{
    private const string MatchColumnName = "MatchText";
    private const string ColorColumnName = "HighlightColor";
    private const string RemoveColumnName = "RemoveRule";

    private readonly TextBox foliaPath = new();
    private readonly TextBox velocityPath = new();
    private readonly CheckBox autoRefresh = new();
    private readonly NumericUpDown autoRefreshMinutes = new();
    private readonly DataGridView highlightRules = new();

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Settings = settings;
        Text = "NordControl UI settings";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 570);
        MinimumSize = new Size(680, 510);
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(246, 244, 234);
        Font = new Font("Segoe UI", 9.5F);

        var layout = BuildLayout(settings);
        Controls.Add(layout);
    }

    private TableLayoutPanel BuildLayout(AppSettings settings)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 3,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var note = new Label
        {
            Text = "Select both NordControl config.json files. Only these paths are saved; bearer tokens remain in the plugin files.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(77, 91, 82),
            AutoEllipsis = true
        };
        layout.Controls.Add(note, 0, 0);
        layout.SetColumnSpan(note, 3);

        AddPathRow(layout, 1, "Folia / Paper", foliaPath, settings.FoliaConfigPath);
        AddPathRow(layout, 2, "Velocity", velocityPath, settings.VelocityConfigPath);
        layout.Controls.Add(BuildAutoRefreshRow(settings), 0, 3);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 3)!, 3);

        var highlightHeading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 4)
        };
        highlightHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        highlightHeading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        highlightHeading.Controls.Add(new Label
        {
            Text = "Log line highlights",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 10F)
        }, 0, 0);
        var addRule = CreateButton("Add rule", false);
        addRule.Click += (_, _) => AddHighlightRule(string.Empty, "White");
        highlightHeading.Controls.Add(addRule, 1, 0);
        layout.Controls.Add(highlightHeading, 0, 4);
        layout.SetColumnSpan(highlightHeading, 3);

        ConfigureHighlightsTable();
        foreach (var rule in settings.EffectiveHighlightRules)
        {
            AddHighlightRule(rule.ContainsText, rule.ColorName);
        }

        layout.Controls.Add(highlightRules, 0, 5);
        layout.SetColumnSpan(highlightRules, 3);
        var hint = new Label
        {
            Text = "Matching is case-insensitive. The first matching rule sets the line color.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(91, 105, 96),
            Font = new Font("Segoe UI", 8.5F)
        };
        layout.Controls.Add(hint, 0, 6);
        layout.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var save = CreateButton("Save", true);
        save.Click += (_, _) => SaveAndClose();
        var cancel = CreateButton("Cancel", false);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 7);
        layout.SetColumnSpan(buttons, 3);

        AcceptButton = save;
        CancelButton = cancel;
        return layout;
    }

    private FlowLayoutPanel BuildAutoRefreshRow(AppSettings settings)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        autoRefresh.Text = "Automatically refresh logs every";
        autoRefresh.AutoSize = true;
        autoRefresh.Checked = settings.AutoRefreshLogs;
        autoRefresh.Margin = new Padding(0, 5, 8, 0);

        autoRefreshMinutes.Minimum = 1;
        autoRefreshMinutes.Maximum = 10_080;
        autoRefreshMinutes.Value = Math.Clamp(settings.AutoRefreshMinutes, 1, 10_080);
        autoRefreshMinutes.Width = 86;
        autoRefreshMinutes.Enabled = autoRefresh.Checked;
        autoRefreshMinutes.Margin = new Padding(0, 1, 6, 0);
        autoRefresh.CheckedChanged += (_, _) => autoRefreshMinutes.Enabled = autoRefresh.Checked;

        row.Controls.Add(autoRefresh);
        row.Controls.Add(autoRefreshMinutes);
        row.Controls.Add(new Label
        {
            Text = "minutes",
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 0)
        });
        return row;
    }

    private void ConfigureHighlightsTable()
    {
        highlightRules.Dock = DockStyle.Fill;
        highlightRules.AllowUserToAddRows = false;
        highlightRules.AllowUserToDeleteRows = false;
        highlightRules.AllowUserToResizeRows = false;
        highlightRules.AutoGenerateColumns = false;
        highlightRules.BackgroundColor = Color.White;
        highlightRules.BorderStyle = BorderStyle.FixedSingle;
        highlightRules.RowHeadersVisible = false;
        highlightRules.SelectionMode = DataGridViewSelectionMode.CellSelect;
        highlightRules.MultiSelect = false;
        highlightRules.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        highlightRules.RowTemplate.Height = 32;

        highlightRules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = MatchColumnName,
            HeaderText = "Contains text",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 65
        });
        highlightRules.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = ColorColumnName,
            HeaderText = "Color",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 25,
            DataSource = LogHighlightPalette.ColorNames.ToArray(),
            FlatStyle = FlatStyle.Flat
        });
        highlightRules.Columns.Add(new DataGridViewButtonColumn
        {
            Name = RemoveColumnName,
            HeaderText = string.Empty,
            Text = "Remove",
            UseColumnTextForButtonValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
        });

        highlightRules.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && highlightRules.Columns[e.ColumnIndex].Name == RemoveColumnName)
            {
                highlightRules.Rows.RemoveAt(e.RowIndex);
            }
        };
        highlightRules.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || highlightRules.Columns[e.ColumnIndex].Name != ColorColumnName)
            {
                return;
            }

            var color = LogHighlightPalette.Resolve(e.Value?.ToString());
            var style = e.CellStyle ?? new DataGridViewCellStyle();
            e.CellStyle = style;
            style.BackColor = color;
            style.ForeColor = Color.FromArgb(18, 31, 24);
            style.SelectionBackColor = color;
            style.SelectionForeColor = Color.FromArgb(18, 31, 24);
        };
        highlightRules.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && highlightRules.Columns[e.ColumnIndex].Name == ColorColumnName)
            {
                highlightRules.InvalidateCell(e.ColumnIndex, e.RowIndex);
            }
        };
        highlightRules.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (highlightRules.IsCurrentCellDirty)
            {
                highlightRules.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    private void AddHighlightRule(string text, string colorName)
    {
        var selectedColor = LogHighlightPalette.ColorNames.Contains(colorName, StringComparer.Ordinal)
            ? colorName
            : "White";
        var rowIndex = highlightRules.Rows.Add(text, selectedColor);
        highlightRules.CurrentCell = highlightRules.Rows[rowIndex].Cells[MatchColumnName];
    }

    private void AddPathRow(TableLayoutPanel layout, int row, string label, TextBox textBox, string value)
    {
        layout.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        textBox.Text = value;
        textBox.PlaceholderText = "Select config.json";
        textBox.Dock = DockStyle.Fill;
        layout.Controls.Add(textBox, 1, row);
        var browse = CreateButton("Browse...", false);
        browse.Click += (_, _) => BrowseFor(textBox);
        layout.Controls.Add(browse, 2, row);
    }

    private void BrowseFor(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select NordControl config.json",
            Filter = "JSON configuration (config.json)|config.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = Path.GetFileName(target.Text)
        };
        var directory = Path.GetDirectoryName(target.Text);
        if (Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(foliaPath.Text) || string.IsNullOrWhiteSpace(velocityPath.Text))
        {
            MessageBox.Show(
                this,
                "Both configuration paths are required.",
                "Invalid settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        highlightRules.EndEdit();
        var rules = new List<LogHighlightRule>();
        foreach (DataGridViewRow row in highlightRules.Rows)
        {
            var text = row.Cells[MatchColumnName].Value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var colorName = row.Cells[ColorColumnName].Value?.ToString() ?? "White";
            rules.Add(new LogHighlightRule(text, colorName));
        }

        Settings = new AppSettings(
            foliaPath.Text.Trim(),
            velocityPath.Text.Trim(),
            rules,
            autoRefresh.Checked,
            Decimal.ToInt32(autoRefreshMinutes.Value));
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button CreateButton(string text, bool primary) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(90, 32),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(31, 76, 57) : Color.White,
        ForeColor = primary ? Color.White : Color.FromArgb(31, 76, 57),
        Cursor = Cursors.Hand
    };
}
