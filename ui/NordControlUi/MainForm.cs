namespace NordControlUi;

internal sealed class MainForm : Form
{
    private static readonly Color Forest = Color.FromArgb(26, 67, 50);
    private static readonly Color Cream = Color.FromArgb(246, 244, 234);
    private static readonly Color Muted = Color.FromArgb(91, 105, 96);
    private static readonly Color Gold = Color.FromArgb(184, 139, 76);
    private static readonly Color Success = Color.FromArgb(41, 153, 105);
    private static readonly Color Failure = Color.FromArgb(199, 68, 74);
    private static readonly Color LogBackground = Color.FromArgb(13, 20, 17);
    private static readonly Color LogForeground = Color.FromArgb(226, 235, 228);

    private readonly TextBox commandInput = new();
    private readonly Button foliaButton;
    private readonly Button velocityButton;
    private readonly Button executeButton;
    private readonly Button logsButton;
    private readonly Button settingsButton;
    private readonly Label statusLabel = new();
    private readonly Panel statusDot = new();
    private readonly RichTextBox output = new();
    private readonly NordControlClient client = new();
    private readonly System.Windows.Forms.Timer autoRefreshTimer = new();
    private AppSettings settings;
    private TargetServer selectedServer = TargetServer.Folia;
    private bool operationInProgress;

    public MainForm()
    {
        settings = SettingsStore.Load();
        Text = "NordControl UI";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(960, 600);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Cream;
        ForeColor = Forest;
        Font = new Font("Segoe UI", 10F);

        foliaButton = CreateButton("Folia / Paper", true);
        velocityButton = CreateButton("Velocity", false);
        executeButton = CreateButton("Execute", true);
        logsButton = CreateDarkButton("Refresh logs");
        settingsButton = CreateButton("Settings", false);

        BuildLayout();
        WireEvents();
        UpdateTargetButtons();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (settings.IsComplete)
        {
            _ = RefreshStatusAsync();
            ConfigureAutoRefresh();
            return;
        }

        SetStatus("Setup required", Gold);
        BeginInvoke(OpenSettings);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            autoRefreshTimer.Dispose();
            client.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.Controls.Add(BuildControlsPanel(), 0, 0);
        root.Controls.Add(BuildLogsPanel(), 1, 0);
        Controls.Add(root);
    }

    private RoundedPanel BuildControlsPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 18,
            Margin = new Padding(0, 0, 7, 0),
            Padding = new Padding(22, 18, 22, 16)
        };
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0),
            ColumnCount = 1,
            RowCount = 6
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        left.Controls.Add(BuildHeading(), 0, 0);
        left.Controls.Add(BuildTargetCard(), 0, 1);
        left.Controls.Add(BuildCommandCard(), 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };
        actions.Controls.Add(settingsButton);
        left.Controls.Add(actions, 0, 3);

        left.Controls.Add(new Label
        {
            Text = "Commands run with full console permissions and are written to the NordControl audit log.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Padding = new Padding(0, 0, 8, 8)
        }, 0, 5);

        panel.Controls.Add(left);
        return panel;
    }

    private static TableLayoutPanel BuildHeading()
    {
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = "NordControl UI",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Forest,
            Dock = DockStyle.Fill
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "Run Minecraft console commands through the local NordControl bridge.",
            ForeColor = Muted,
            Dock = DockStyle.Fill
        }, 0, 1);
        return heading;
    }

    private Panel BuildTargetCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 12),
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "Target server",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F)
        };
        layout.Controls.Add(label, 0, 0);

        var status = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0)
        };
        statusDot.Size = new Size(10, 10);
        statusDot.BackColor = Gold;
        statusDot.Margin = new Padding(0, 5, 7, 0);
        statusLabel.Text = "Checking...";
        statusLabel.ForeColor = Muted;
        statusLabel.AutoSize = true;
        status.Controls.Add(statusDot);
        status.Controls.Add(statusLabel);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        layout.Controls.Add(status, 1, 0);

        foliaButton.Dock = DockStyle.Fill;
        foliaButton.Margin = new Padding(0, 4, 6, 0);
        velocityButton.Dock = DockStyle.Fill;
        velocityButton.Margin = new Padding(6, 4, 0, 0);
        layout.Controls.Add(foliaButton, 0, 1);
        layout.Controls.Add(velocityButton, 1, 1);

        card.Controls.Add(layout);
        return card;
    }

    private Panel BuildCommandCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 14),
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "Console command",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F)
        };
        layout.Controls.Add(label, 0, 0);
        layout.SetColumnSpan(label, 2);

        commandInput.PlaceholderText = "Example: list";
        commandInput.Dock = DockStyle.Fill;
        commandInput.Font = new Font("Cascadia Mono", 10F);
        commandInput.Margin = new Padding(0, 4, 12, 4);
        layout.Controls.Add(commandInput, 0, 1);

        executeButton.Dock = DockStyle.Fill;
        executeButton.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(executeButton, 1, 1);

        card.Controls.Add(layout);
        return card;
    }

    private RoundedPanel BuildLogsPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = LogBackground,
            CornerRadius = 18,
            Margin = new Padding(7, 0, 0, 0),
            Padding = new Padding(18, 14, 18, 16)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = LogBackground,
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Logs",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = LogBackground,
            Margin = new Padding(0, 3, 0, 0)
        };
        actions.Controls.Add(logsButton);
        layout.Controls.Add(actions, 1, 0);

        output.ReadOnly = true;
        output.BackColor = LogBackground;
        output.ForeColor = LogForeground;
        output.BorderStyle = BorderStyle.None;
        output.Font = new Font("Cascadia Mono", 9.5F);
        output.Dock = DockStyle.Fill;
        output.DetectUrls = false;
        output.WordWrap = true;
        output.ScrollBars = RichTextBoxScrollBars.Vertical;
        output.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(output, 0, 1);
        layout.SetColumnSpan(output, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private void WireEvents()
    {
        foliaButton.Click += async (_, _) => await SelectServerAsync(TargetServer.Folia);
        velocityButton.Click += async (_, _) => await SelectServerAsync(TargetServer.Velocity);
        executeButton.Click += async (_, _) => await ExecuteCommandAsync();
        logsButton.Click += async (_, _) => await RefreshLogsAsync();
        settingsButton.Click += (_, _) => OpenSettings();
        autoRefreshTimer.Tick += async (_, _) =>
        {
            if (!operationInProgress && settings.IsComplete)
            {
                await RefreshLogsAsync();
            }
        };
        commandInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await ExecuteCommandAsync();
            }
        };
    }

    private async Task SelectServerAsync(TargetServer server)
    {
        if (selectedServer == server)
        {
            return;
        }

        selectedServer = server;
        UpdateTargetButtons();
        await RefreshStatusAsync();
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
            await client.CheckHealthAsync(LoadSelectedProfile());
            SetStatus("Connected", Success);
        }
        catch (Exception)
        {
            SetStatus("Unavailable", Failure);
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

        SetBusy(true);
        Append("COMMAND", $"{profile.DisplayName} > {command}");
        try
        {
            var result = await client.RunCommandAsync(profile, command);
            if (result.IsError)
            {
                Append("ERROR", result.Format());
            }

            commandInput.SelectAll();
            await Task.Delay(650);
            await ReplaceLogsAsync(profile);
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

    private async Task RefreshLogsAsync()
    {
        SetBusy(true);
        output.Clear();
        try
        {
            var profile = LoadSelectedProfile();
            await ReplaceLogsAsync(profile);
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

    private async Task ReplaceLogsAsync(ConnectionProfile profile)
    {
        var logs = await client.GetRecentLogsAsync(profile);
        output.Clear();
        Append(logs.IsError ? "ERROR" : "RECENT LOGS", logs.Format());
    }

    private ConnectionProfile LoadSelectedProfile()
    {
        if (!settings.IsComplete)
        {
            throw new InvalidOperationException("Select both NordControl config.json files in Settings first.");
        }

        var isVelocity = selectedServer == TargetServer.Velocity;
        return ConnectionProfileLoader.Load(
            isVelocity ? "Velocity" : "Folia / Paper",
            isVelocity ? settings.VelocityConfigPath : settings.FoliaConfigPath);
    }

    private void OpenSettings()
    {
        autoRefreshTimer.Stop();
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            ConfigureAutoRefresh();
            return;
        }

        try
        {
            SettingsStore.Save(dialog.Settings);
            settings = dialog.Settings;
            ReapplyLogHighlights();
            ConfigureAutoRefresh();
            _ = RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            ConfigureAutoRefresh();
            ShowError("Settings could not be saved: " + exception.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        operationInProgress = busy;
        executeButton.Enabled = !busy;
        logsButton.Enabled = !busy;
        settingsButton.Enabled = !busy;
        foliaButton.Enabled = !busy;
        velocityButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ConfigureAutoRefresh()
    {
        autoRefreshTimer.Stop();
        if (!settings.AutoRefreshLogs)
        {
            return;
        }

        var minutes = Math.Clamp(settings.AutoRefreshMinutes, 1, 10_080);
        autoRefreshTimer.Interval = checked(minutes * 60_000);
        autoRefreshTimer.Start();
    }

    private void SetStatus(string text, Color color)
    {
        statusLabel.Text = text;
        statusDot.BackColor = color;
    }

    private void UpdateTargetButtons()
    {
        ApplyTargetButtonStyle(foliaButton, selectedServer == TargetServer.Folia);
        ApplyTargetButtonStyle(velocityButton, selectedServer == TargetServer.Velocity);
    }

    private void Append(string category, string message)
    {
        AppendLine($"[{DateTime.Now:HH:mm:ss}] {category}");
        foreach (var line in message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            AppendLine(line);
        }

        AppendLine(string.Empty);
        output.SelectionStart = output.TextLength;
        output.ScrollToCaret();
    }

    private void AppendLine(string line)
    {
        output.SelectionStart = output.TextLength;
        output.SelectionLength = 0;
        output.SelectionColor = ResolveLineColor(line);
        output.AppendText(line + Environment.NewLine);
        output.SelectionColor = LogForeground;
    }

    private Color ResolveLineColor(string line)
    {
        foreach (var rule in settings.EffectiveHighlightRules)
        {
            if (!string.IsNullOrWhiteSpace(rule.ContainsText) &&
                line.Contains(rule.ContainsText, StringComparison.OrdinalIgnoreCase))
            {
                return LogHighlightPalette.Resolve(rule.ColorName);
            }
        }

        return LogForeground;
    }

    private void ReapplyLogHighlights()
    {
        var text = output.Text;
        output.Clear();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'))
        {
            AppendLine(line);
        }
    }

    private void ShowError(string message) => MessageBox.Show(
        this,
        message,
        "NordControl UI",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);

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
            Size = new Size(112, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Forest : Color.White,
            ForeColor = primary ? Color.White : Forest,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Forest : Color.FromArgb(190, 202, 191);
        return button;
    }

    private static Button CreateDarkButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(108, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 36, 31),
            ForeColor = Color.FromArgb(224, 233, 226),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(65, 82, 73);
        return button;
    }

    private static void ApplyTargetButtonStyle(Button button, bool selected)
    {
        button.BackColor = selected ? Forest : Color.White;
        button.ForeColor = selected ? Color.White : Forest;
        button.FlatAppearance.BorderColor = selected ? Forest : Color.FromArgb(190, 202, 191);
    }

    private enum TargetServer
    {
        Folia,
        Velocity
    }
}
