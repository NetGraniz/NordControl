namespace NordControlUi;

internal sealed class MainForm : Form
{
    private static readonly Color Forest = Color.FromArgb(26, 67, 50);
    private static readonly Color Cream = Color.FromArgb(246, 244, 234);
    private static readonly Color Muted = Color.FromArgb(91, 105, 96);
    private static readonly Color Gold = Color.FromArgb(184, 139, 76);
    private static readonly Color Success = Color.FromArgb(41, 153, 105);
    private static readonly Color Failure = Color.FromArgb(199, 68, 74);

    private readonly ComboBox serverSelector = new();
    private readonly TextBox commandInput = new();
    private readonly Button executeButton;
    private readonly Button logsButton;
    private readonly Button settingsButton;
    private readonly Button clearButton;
    private readonly Label statusLabel = new();
    private readonly Panel statusDot = new();
    private readonly RichTextBox output = new();
    private readonly NordControlClient client = new();
    private AppSettings settings;

    public MainForm()
    {
        settings = SettingsStore.Load();
        Text = "NordControl UI";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(720, 520);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Cream;
        ForeColor = Forest;
        Font = new Font("Segoe UI", 10F);

        executeButton = CreateButton("Execute", true);
        logsButton = CreateButton("Refresh logs", false);
        settingsButton = CreateButton("Settings", false);
        clearButton = CreateButton("Clear", false);

        BuildLayout();
        WireEvents();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (settings.IsComplete)
        {
            _ = RefreshStatusAsync();
            return;
        }

        SetStatus("Setup required", Gold);
        BeginInvoke(OpenSettings);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            client.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 20),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titleBlock = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        titleBlock.Controls.Add(new Label
        {
            Text = "NordControl UI",
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Forest,
            Dock = DockStyle.Fill
        }, 0, 0);
        titleBlock.Controls.Add(new Label
        {
            Text = "Run an audited Minecraft console command through the local NordControl bridge.",
            ForeColor = Muted,
            Dock = DockStyle.Fill
        }, 0, 1);
        heading.Controls.Add(titleBlock, 0, 0);
        settingsButton.Margin = new Padding(8, 8, 0, 0);
        heading.Controls.Add(settingsButton, 1, 0);
        root.Controls.Add(heading, 0, 0);

        var targetCard = CreateCard();
        var targetLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 10), ColumnCount = 5 };
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        targetLayout.Controls.Add(new Label { Text = "Target server", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10F) }, 0, 0);
        serverSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        serverSelector.Items.AddRange(["Folia / Paper", "Velocity"]);
        serverSelector.SelectedIndex = 0;
        serverSelector.Dock = DockStyle.Fill;
        serverSelector.Margin = new Padding(0, 4, 12, 4);
        targetLayout.Controls.Add(serverSelector, 1, 0);
        statusDot.Size = new Size(10, 10);
        statusDot.BackColor = Gold;
        statusDot.Margin = new Padding(8, 17, 8, 0);
        targetLayout.Controls.Add(statusDot, 3, 0);
        statusLabel.Text = "Checking...";
        statusLabel.ForeColor = Muted;
        statusLabel.AutoSize = true;
        statusLabel.Margin = new Padding(0, 12, 0, 0);
        targetLayout.Controls.Add(statusLabel, 4, 0);
        targetCard.Controls.Add(targetLayout);
        root.Controls.Add(targetCard, 0, 1);

        var commandCard = CreateCard();
        var commandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 12), ColumnCount = 2, RowCount = 2 };
        commandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        commandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        commandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        commandLayout.Controls.Add(new Label { Text = "Console command", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F) }, 0, 0);
        commandLayout.SetColumnSpan(commandLayout.GetControlFromPosition(0, 0)!, 2);
        commandInput.PlaceholderText = "Example: list";
        commandInput.Dock = DockStyle.Fill;
        commandInput.Font = new Font("Cascadia Mono", 10F);
        commandInput.Margin = new Padding(0, 2, 12, 0);
        commandLayout.Controls.Add(commandInput, 0, 1);
        executeButton.Dock = DockStyle.Fill;
        executeButton.Margin = new Padding(0, 2, 0, 0);
        commandLayout.Controls.Add(executeButton, 1, 1);
        commandCard.Controls.Add(commandLayout);
        root.Controls.Add(commandCard, 0, 2);

        output.ReadOnly = true;
        output.BackColor = Color.FromArgb(18, 40, 31);
        output.ForeColor = Color.FromArgb(221, 231, 222);
        output.BorderStyle = BorderStyle.None;
        output.Font = new Font("Cascadia Mono", 9.5F);
        output.Dock = DockStyle.Fill;
        output.DetectUrls = false;
        output.WordWrap = false;
        root.Controls.Add(output, 0, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0) };
        actions.Controls.Add(logsButton);
        actions.Controls.Add(clearButton);
        root.Controls.Add(actions, 0, 4);

        root.Controls.Add(new Label
        {
            Text = "Commands run with full console permissions and are written to the NordControl audit log.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F)
        }, 0, 5);

        Controls.Add(root);
    }

    private void WireEvents()
    {
        serverSelector.SelectedIndexChanged += async (_, _) => await RefreshStatusAsync();
        executeButton.Click += async (_, _) => await ExecuteCommandAsync();
        logsButton.Click += async (_, _) => await RefreshLogsAsync();
        clearButton.Click += (_, _) => output.Clear();
        settingsButton.Click += (_, _) => OpenSettings();
        commandInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await ExecuteCommandAsync();
            }
        };
    }

    private async Task RefreshStatusAsync()
    {
        if (!settings.IsComplete)
        {
            SetStatus("Setup required", Gold);
            return;
        }

        SetBusy(true);
        SetStatus("Checking...", Gold);
        try
        {
            var profile = LoadSelectedProfile();
            await client.CheckHealthAsync(profile);
            SetStatus("Connected", Success);
        }
        catch (Exception exception)
        {
            SetStatus("Unavailable", Failure);
            Append("ERROR", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExecuteCommandAsync()
    {
        string command;
        ConnectionProfile profile;
        try
        {
            command = CommandValidator.Normalize(commandInput.Text);
            profile = LoadSelectedProfile();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Target: {profile.DisplayName}\nCommand: {command}\n\nRun this command with full console permissions?",
            "Confirm console command",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);
        Append("COMMAND", $"{profile.DisplayName} > {command}");
        try
        {
            var result = await client.RunCommandAsync(profile, command);
            Append(result.IsError ? "ERROR" : "DISPATCHED", result.Format());
            commandInput.SelectAll();
            await Task.Delay(650);
            await AppendLogsAsync(profile);
            SetStatus("Connected", Success);
        }
        catch (Exception exception)
        {
            SetStatus("Unavailable", Failure);
            Append("ERROR", exception.Message);
            ShowError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshLogsAsync()
    {
        SetBusy(true);
        try
        {
            var profile = LoadSelectedProfile();
            await AppendLogsAsync(profile);
            SetStatus("Connected", Success);
        }
        catch (Exception exception)
        {
            SetStatus("Unavailable", Failure);
            Append("ERROR", exception.Message);
            ShowError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task AppendLogsAsync(ConnectionProfile profile)
    {
        var logs = await client.GetRecentLogsAsync(profile);
        Append(logs.IsError ? "ERROR" : "RECENT LOGS", logs.Format());
    }

    private ConnectionProfile LoadSelectedProfile()
    {
        if (!settings.IsComplete)
        {
            throw new InvalidOperationException("Select both NordControl config.json files in Settings first.");
        }

        var isVelocity = serverSelector.SelectedIndex == 1;
        return ConnectionProfileLoader.Load(
            isVelocity ? "Velocity" : "Folia / Paper",
            isVelocity ? settings.VelocityConfigPath : settings.FoliaConfigPath);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SettingsStore.Save(dialog.Settings);
            settings = dialog.Settings;
            Append("SETTINGS", "Configuration paths updated. No bearer token was stored by NordControl UI.");
            _ = RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            ShowError("Settings could not be saved: " + exception.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        executeButton.Enabled = !busy;
        logsButton.Enabled = !busy;
        settingsButton.Enabled = !busy;
        serverSelector.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void SetStatus(string text, Color color)
    {
        statusLabel.Text = text;
        statusDot.BackColor = color;
    }

    private void Append(string category, string message)
    {
        output.AppendText($"[{DateTime.Now:HH:mm:ss}] {category}\r\n{message}\r\n\r\n");
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private void ShowError(string message) => MessageBox.Show(this, message, "NordControl UI", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static Panel CreateCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        Margin = new Padding(0, 4, 0, 8),
        Padding = new Padding(1)
    };

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(112, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Forest : Color.White,
            ForeColor = primary ? Color.White : Forest,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Forest : Color.FromArgb(190, 202, 191);
        return button;
    }
}
