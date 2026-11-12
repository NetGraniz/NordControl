namespace NordControlUi;

internal sealed class SettingsForm : Form
{
    private readonly TextBox foliaPath = new();
    private readonly TextBox velocityPath = new();

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Settings = settings;
        Text = "NordControl UI settings";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 230);
        MinimumSize = new Size(600, 260);
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(246, 244, 234);
        Font = new Font("Segoe UI", 9.5F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 3,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var note = new Label
        {
            Text = "Only configuration paths are saved. Bearer tokens remain in the plugin files.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(77, 91, 82),
            AutoEllipsis = true
        };
        layout.Controls.Add(note, 0, 0);
        layout.SetColumnSpan(note, 3);

        AddPathRow(layout, 1, "Folia / Paper", foliaPath, settings.FoliaConfigPath);
        AddPathRow(layout, 2, "Velocity", velocityPath, settings.VelocityConfigPath);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = CreateButton("Save", true);
        save.Click += (_, _) => SaveAndClose();
        var cancel = CreateButton("Cancel", false);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 3);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private void AddPathRow(TableLayoutPanel layout, int row, string label, TextBox textBox, string value)
    {
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        textBox.Text = value;
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
            MessageBox.Show(this, "Both configuration paths are required.", "Invalid settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings = new AppSettings(foliaPath.Text.Trim(), velocityPath.Text.Trim());
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
        ForeColor = primary ? Color.White : Color.FromArgb(31, 76, 57)
    };
}
