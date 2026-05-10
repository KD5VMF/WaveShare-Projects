using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AdGuardLiveWatch;

public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly BindingList<QueryRow> _rows = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string _settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AGWatch");
    private readonly string _legacySettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdGuardLiveWatch");
    private string SettingsFile => Path.Combine(_settingsDir, "settings.json");

    private AppSettings _settings = new();
    private string _runtimePassword = string.Empty;
    private CancellationTokenSource? _pollCts;

    private DateTime _lastStatTimeUtc = DateTime.MinValue;
    private long _lastTotalQueries;
    private long _lastBlockedTotal;
    private double _qpsPeak = 20;
    private double _bpsPeak = 5;
    private string _lastGridSignature = "";
    private readonly Dictionary<string, ServerRuntime> _serverRuntime = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<double> _qpsHistory = new();
    private readonly List<double> _blockPctHistory = new();
    private List<BarItem> _statsTopBlocked = new();
    private List<BarItem> _statsTopClients = new();
    private List<BarItem> _statsTopQueried = new();

    private TextBox txtUrl = null!;
    private TextBox txtUrl2 = null!;
    private TextBox txtUser = null!;
    private TextBox txtPass = null!;
    private NumericUpDown nudPoll = null!;
    private NumericUpDown nudLimit = null!;
    private CheckBox chkAuto = null!;
    private CheckBox chkPrimary = null!;
    private CheckBox chkSecond = null!;
    private Button btnStart = null!;
    private Button btnStop = null!;
    private Label lblStatus = null!;
    private Button btnFullScreen = null!;
    private bool _isFullScreen = false;
    private FormBorderStyle _savedBorderStyle;
    private FormWindowState _savedWindowState;
    private Rectangle _savedBounds;
    private bool _updatingDnsCheckboxes;
    private bool _loadingSettingsToUi;

    private StatCard cardTotal = null!;
    private StatCard cardBlocked = null!;
    private StatCard cardBlockPct = null!;
    private StatCard cardLoad = null!;
    private StatCard cardClients = null!;
    private StatCard cardLatency = null!;
    private StatCard cardProtection = null!;
    private StatCard cardLast = null!;

    private SmoothBarGraph graphQps = null!;
    private SmoothBarGraph graphBlockPct = null!;
    private LedMeter meterQps = null!;
    private LedMeter meterBps = null!;
    private HorizontalBarList barsClients = null!;
    private HorizontalBarList barsBlocked = null!;
    private DataGridView grid = null!;

    private sealed class ServerTarget
    {
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public Color Accent { get; set; } = Theme.Blue;
    }

    private sealed class ServerRuntime
    {
        public DateTime LastStatTimeUtc { get; set; } = DateTime.MinValue;
        public long LastTotalQueries { get; set; }
        public long LastBlockedTotal { get; set; }
        public double LastQps { get; set; }
        public double LastBps { get; set; }
        public long LastDeltaQueries { get; set; }
        public long LastDeltaBlocked { get; set; }
        public bool ProtectionOn { get; set; }
        public bool Online { get; set; }
        public string Error { get; set; } = "";
    }

    private sealed class ServerPollResult
    {
        public ServerTarget Target { get; set; } = new();
        public bool Success { get; set; }
        public string Error { get; set; } = "";
        public bool ProtectionOn { get; set; }
        public long TotalQueries { get; set; }
        public long BlockedQueries { get; set; }
        public double OverallBlockPct => TotalQueries <= 0 ? 0 : BlockedQueries * 100.0 / TotalQueries;
        public double Qps { get; set; }
        public double Bps { get; set; }
        public long DeltaQueries { get; set; }
        public long DeltaBlocked { get; set; }
        public List<QueryRow> Rows { get; set; } = new();
        public List<BarItem> TopClients { get; set; } = new();
        public List<BarItem> TopBlocked { get; set; } = new();
        public List<BarItem> TopQueried { get; set; } = new();
    }

    public MainForm()
    {
        Text = "AGWatch REV16 Auto DNS Save";
        Width = 1620;
        Height = 980;
        MinimumSize = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Theme.Back;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10);

        LoadSettings();
        BuildUi();
        ApplySettingsToUi();
        SaveSettingsFromUi();
        SaveSettings();

        _timer.Tick += async (_, _) => await PollOnceAsync();

        Shown += (_, _) =>
        {
            WinFormsTuning.EnableDoubleBuffering(this);
            if (_settings.AutoStart)
                StartPolling();
        };

        FormClosing += (_, _) =>
        {
            SaveSettingsFromUi();
            SaveSettings();
            _pollCts?.Cancel();
            _http.Dispose();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
            BackColor = Theme.Back
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 238));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildTop(), 0, 0);
        root.Controls.Add(BuildCards(), 0, 1);
        root.Controls.Add(BuildGraphs(), 0, 2);
        root.Controls.Add(BuildBars(), 0, 3);
        root.Controls.Add(BuildGrid(), 0, 4);
    }

    private Control BuildTop()
    {
        var p = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card2,
            Padding = new Padding(10),
            ColumnCount = 15,
            RowCount = 2
        };

        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        p.Controls.Add(MakeLabel("DNS1 API\nURL:PORT"), 0, 0);
        txtUrl = MakeTextBox();
        p.Controls.Add(txtUrl, 1, 0);

        p.Controls.Add(MakeLabel("User"), 2, 0);
        txtUser = MakeTextBox();
        p.Controls.Add(txtUser, 3, 0);

        p.Controls.Add(MakeLabel("Pass"), 4, 0);
        txtPass = MakeTextBox();
        txtPass.UseSystemPasswordChar = true;
        p.Controls.Add(txtPass, 5, 0);

        p.Controls.Add(MakeTightLabel("Poll"), 6, 0);
        nudPoll = MakeNum(1, 30, 5, 1);
        p.Controls.Add(nudPoll, 7, 0);

        p.Controls.Add(MakeTightLabel("Rows"), 8, 0);
        nudLimit = MakeNum(50, 500, 100, 10);
        p.Controls.Add(nudLimit, 9, 0);

        btnStart = MakeButton("START", Theme.Green);
        btnStart.Click += (_, _) => StartPolling();
        p.Controls.Add(btnStart, 10, 0);

        btnStop = MakeButton("STOP", Theme.Red);
        btnStop.Click += (_, _) => StopPolling();
        p.Controls.Add(btnStop, 11, 0);

        var btnWeb1 = MakeButton("OPEN DNS1", Theme.Blue);
        btnWeb1.Click += (_, _) => OpenAdGuardUi(txtUrl.Text);
        p.Controls.Add(btnWeb1, 12, 0);

        btnFullScreen = MakeButton("FULLSCREEN", Theme.Purple);
        btnFullScreen.Click += (_, _) => ToggleFullScreen();
        p.Controls.Add(btnFullScreen, 13, 0);

        p.Controls.Add(MakeLabel("DNS2 API\nURL:PORT"), 0, 1);
        txtUrl2 = MakeTextBox();
        p.Controls.Add(txtUrl2, 1, 1);

        chkPrimary = new CheckBox
        {
            Text = "Use DNS1",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Checked = true
        };
        chkPrimary.CheckedChanged += (_, _) => EnsureAtLeastOneDnsEnabled(chkPrimary);
        p.Controls.Add(chkPrimary, 2, 1);
        p.SetColumnSpan(chkPrimary, 2);

        chkSecond = new CheckBox
        {
            Text = "Use DNS2",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Checked = true
        };
        chkSecond.CheckedChanged += (_, _) => EnsureAtLeastOneDnsEnabled(chkSecond);
        p.Controls.Add(chkSecond, 4, 1);
        p.SetColumnSpan(chkSecond, 2);

        chkAuto = new CheckBox
        {
            Text = "Auto start",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Checked = false
        };
        p.Controls.Add(chkAuto, 6, 1);
        p.SetColumnSpan(chkAuto, 2);

        lblStatus = new Label
        {
            Text = "Ready — toggle DNS1 or DNS2 to test each server; one must stay enabled",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Blue,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        p.Controls.Add(lblStatus, 8, 1);
        p.SetColumnSpan(lblStatus, 4);

        var btnWeb2 = MakeButton("OPEN DNS2", Theme.Blue);
        btnWeb2.Click += (_, _) => OpenAdGuardUi(txtUrl2.Text);
        p.Controls.Add(btnWeb2, 12, 1);

        var btnBoth = MakeButton("OPEN BOTH", Theme.Orange);
        btnBoth.Click += (_, _) => { OpenAdGuardUi(txtUrl.Text); OpenAdGuardUi(txtUrl2.Text); };
        p.Controls.Add(btnBoth, 13, 1);

        txtUrl.TextChanged += (_, _) => OnDnsAddressChanged("DNS1");
        txtUrl2.TextChanged += (_, _) => OnDnsAddressChanged("DNS2");
        txtUser.TextChanged += (_, _) => AutoSaveSettingsAndRefreshCards();
        txtPass.TextChanged += (_, _) => AutoSaveSettingsAndRefreshCards();
        nudPoll.ValueChanged += (_, _) => AutoSaveSettingsAndRefreshCards();
        nudLimit.ValueChanged += (_, _) => AutoSaveSettingsAndRefreshCards();
        chkAuto.CheckedChanged += (_, _) => AutoSaveSettingsAndRefreshCards();

        return p;
    }

    private Control BuildCards()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Back, ColumnCount = 4, RowCount = 2 };
        for (int i = 0; i < 4; i++) p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        cardTotal = AddCard(p, 0, 0, "TOTAL DNS", "0", "queries", Theme.Blue);
        cardBlocked = AddCard(p, 1, 0, "BLOCKED", "0", "blocked queries", Theme.Red);
        cardBlockPct = AddCard(p, 2, 0, "BLOCK RATE", "0.0%", "ratio", Theme.Orange);
        cardLoad = AddCard(p, 3, 0, "LIVE LOAD", "0.0 q/s", "queries per second", Theme.Green);
        cardClients = AddCard(p, 0, 1, "DNS1", "0.0 q/s", "192.168.1.206:80", Theme.Blue);
        cardLatency = AddCard(p, 1, 1, "DNS2", "0.0 q/s", "192.168.1.207:3000", Theme.Purple);
        cardProtection = AddCard(p, 2, 1, "ACTIVE DNS NOW", "?", "server split", Theme.Green);
        cardLast = AddCard(p, 3, 1, "LAST UPDATE", "never", "local time", Theme.Blue);
        return p;
    }

    private Control BuildGraphs()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Back, ColumnCount = 4, RowCount = 1 };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

        graphQps = new SmoothBarGraph { Title = "QUERY LOAD HISTORY", AccentColor = Theme.Blue };
        graphBlockPct = new SmoothBarGraph
        {
            Title = "LIVE BLOCK RATE HISTORY (DYNAMIC)",
            AccentColor = Theme.Orange,
            UseDynamicRange = true,
            DynamicMinRange = 5.0,
            DynamicPaddingFraction = 0.22,
            ClampMin = 0,
            ClampMax = 100,
            RangeSuffix = "%"
        };
        meterQps = new LedMeter { Title = "", Unit = "q/s", AccentColor = Theme.Green, Segments = 10 };
        meterBps = new LedMeter { Title = "", Unit = "b/s", AccentColor = Theme.Red, Segments = 10 };

        p.Controls.Add(graphQps, 0, 0);
        p.Controls.Add(graphBlockPct, 1, 0);
        p.Controls.Add(MakeMeterPanel("Q/S LOAD", meterQps), 2, 0);
        p.Controls.Add(MakeMeterPanel("BLOCK/S", meterBps), 3, 0);
        return p;
    }


    private Control MakeMeterPanel(string title, LedMeter meter)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            BackColor = Theme.Card
        };

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Muted,
            BackColor = Theme.Card,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding = new Padding(0, 8, 0, 0)
        };

        meter.Dock = DockStyle.Fill;
        meter.Margin = new Padding(0);

        panel.Controls.Add(meter);
        panel.Controls.Add(label);
        return panel;
    }

    private Control BuildBars()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Back, ColumnCount = 2, RowCount = 1 };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        barsClients = new HorizontalBarList { Title = "TOP CLIENTS", AccentColor = Theme.Purple, MaxItems = 7 };
        barsBlocked = new HorizontalBarList { Title = "TOP BLOCKED / FILTERED DOMAINS", AccentColor = Theme.Red, MaxItems = 8 };

        p.Controls.Add(barsClients, 0, 0);
        p.Controls.Add(barsBlocked, 1, 0);
        return p;
    }

    private Control BuildGrid()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(5), BackColor = Theme.Card };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "RECENT QUERY LOG",
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            Font = Theme.SmallBold,
            Padding = new Padding(10, 6, 0, 0)
        };

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Theme.Card,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            DataSource = _rows,
            Margin = new Padding(0)
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(27, 38, 58);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.SmallBold;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
        grid.DefaultCellStyle.ForeColor = Theme.Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(44, 94, 150);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(19, 28, 46);
        grid.RowTemplate.Height = 25;
        grid.CellFormatting += Grid_CellFormatting;

        AddColumn("SourceServer", "DNS BOX", 145);
        AddColumn("TimeLocal", "TIME", 140);
        AddColumn("Client", "CLIENT", 190);
        AddColumn("Domain", "DOMAIN", 440, true);
        AddColumn("Type", "TYPE", 70);
        AddColumn("Status", "STATUS", 105);
        AddColumn("Reason", "REASON", 160);
        AddColumn("Elapsed", "MS", 80);

        panel.Controls.Add(grid);
        panel.Controls.Add(title);
        return panel;
    }

    private StatCard AddCard(TableLayoutPanel parent, int col, int row, string title, string value, string sub, Color accent)
    {
        var c = new StatCard { Title = title, ValueText = value, SubText = sub, AccentColor = accent };
        parent.Controls.Add(c, col, row);
        return c;
    }

    private void AddColumn(string prop, string header, int width, bool fill = false)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = prop,
            HeaderText = header,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        });
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
        var row = _rows[e.RowIndex];
        var style = grid.Rows[e.RowIndex].DefaultCellStyle;
        if (row.Blocked)
        {
            style.BackColor = Color.FromArgb(82, 26, 38);
            style.ForeColor = Color.FromArgb(255, 232, 232);
        }
        else
        {
            style.BackColor = e.RowIndex % 2 == 0 ? Color.FromArgb(15, 23, 42) : Color.FromArgb(19, 28, 46);
            style.ForeColor = Theme.Text;
        }
    }


    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _savedBorderStyle = FormBorderStyle;
            _savedWindowState = WindowState;
            _savedBounds = Bounds;

            SuspendLayout();
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = false;
            _isFullScreen = true;
            btnFullScreen.Text = "WINDOWED";
            ResumeLayout(true);
        }
        else
        {
            SuspendLayout();
            FormBorderStyle = _savedBorderStyle == 0 ? FormBorderStyle.Sizable : _savedBorderStyle;
            WindowState = FormWindowState.Normal;
            Bounds = _savedBounds.Width > 100 ? _savedBounds : Screen.FromControl(this).WorkingArea;
            WindowState = FormWindowState.Maximized;
            TopMost = false;
            _isFullScreen = false;
            btnFullScreen.Text = "FULLSCREEN";
            ResumeLayout(true);
        }
    }

    private void EnsureAtLeastOneDnsEnabled(CheckBox changedBox)
    {
        if (_updatingDnsCheckboxes || chkPrimary == null || chkSecond == null)
            return;

        if (!chkPrimary.Checked && !chkSecond.Checked)
        {
            _updatingDnsCheckboxes = true;
            changedBox.Checked = true;
            _updatingDnsCheckboxes = false;

            if (lblStatus != null)
            {
                lblStatus.Text = "At least one DNS server must stay enabled.";
                lblStatus.ForeColor = Theme.Orange;
            }
        }

        SaveSettingsFromUi();
        SaveSettings();
        RefreshServerIdentityUi();
    }

    private void StartPolling()
    {
        SaveSettingsFromUi();
        SaveSettings();
        ConfigureHttpClient();
        _timer.Interval = _settings.PollSeconds * 1000;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        _timer.Start();
        lblStatus.Text = "Connected / polling";
        lblStatus.ForeColor = Theme.Green;
        _ = PollOnceAsync();
    }

    private void StopPolling()
    {
        _timer.Stop();
        _pollCts?.Cancel();
        btnStart.Enabled = true;
        btnStop.Enabled = false;
        lblStatus.Text = "Stopped";
        lblStatus.ForeColor = Theme.Red;
    }

    private async Task PollOnceAsync()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        try
        {
            SaveSettingsFromUi();
            ConfigureHttpClient();

            var targets = GetServerTargets();
            if (targets.Count == 0)
                throw new InvalidOperationException("No AdGuard servers are enabled.");

            var results = new List<ServerPollResult>();
            foreach (var target in targets)
                results.Add(await PollServerAsync(target, _pollCts.Token));

            ApplyDualResults(results);

            cardLast.ValueText = DateTime.Now.ToString("HH:mm:ss");
            cardLast.SubText = DateTime.Now.ToString("yyyy-MM-dd");

            int online = results.Count(r => r.Success);
            int offline = results.Count - online;
            lblStatus.Text = offline == 0
                ? $"Connected — {online}/{results.Count} DNS servers online"
                : $"Partial — {online}/{results.Count} online, {offline} offline";
            lblStatus.ForeColor = offline == 0 ? Theme.Green : Theme.Orange;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "ERROR: " + TrimForStatus(ex.Message);
            lblStatus.ForeColor = Theme.Red;
        }
    }

    private List<ServerTarget> GetServerTargets()
    {
        var list = new List<ServerTarget>();

        string dns1 = NormalizeBaseUrl(txtUrl.Text, "http://192.168.1.206:80");
        if (chkPrimary.Checked && !string.IsNullOrWhiteSpace(dns1))
            list.Add(new ServerTarget { Name = BuildServerName("DNS1", dns1), BaseUrl = dns1, Accent = Theme.Blue });

        string dns2 = NormalizeBaseUrl(txtUrl2.Text, "http://192.168.1.207:3000");
        if (chkSecond.Checked && !string.IsNullOrWhiteSpace(dns2))
            list.Add(new ServerTarget { Name = BuildServerName("DNS2", dns2), BaseUrl = dns2, Accent = Theme.Purple });

        return list;
    }

    private async Task<ServerPollResult> PollServerAsync(ServerTarget target, CancellationToken token)
    {
        var result = new ServerPollResult { Target = target };

        try
        {
            using var statusDoc = await GetJsonAsync(target.BaseUrl, "/control/status", token);
            using var statsDoc = await GetJsonAsync(target.BaseUrl, "/control/stats", token);
            using var logDoc = await GetJsonAsync(target.BaseUrl, $"/control/querylog?limit={(int)nudLimit.Value}", token);

            result.Success = true;
            result.ProtectionOn = GetFlexibleBool(statusDoc.RootElement, "protection_enabled");
            result.TotalQueries = GetLong(statsDoc.RootElement, "num_dns_queries");
            result.BlockedQueries = GetBlockedTotal(statsDoc.RootElement);
            result.TopClients = ExtractBarItems(statsDoc.RootElement, "top_clients", 12);
            result.TopBlocked = ExtractBarItems(statsDoc.RootElement, "top_blocked_domains", 12);
            result.TopQueried = ExtractBarItems(statsDoc.RootElement, "top_queried_domains", 12);
            result.Rows = ExtractRows(logDoc.RootElement, target.Name);

            var rt = GetRuntime(target.Name);
            var now = DateTime.UtcNow;
            if (rt.LastStatTimeUtc != DateTime.MinValue)
            {
                double secs = Math.Max(0.1, (now - rt.LastStatTimeUtc).TotalSeconds);
                result.DeltaQueries = Math.Max(0, result.TotalQueries - rt.LastTotalQueries);
                result.DeltaBlocked = Math.Max(0, result.BlockedQueries - rt.LastBlockedTotal);
                result.Qps = result.DeltaQueries / secs;
                result.Bps = result.DeltaBlocked / secs;
            }

            rt.LastStatTimeUtc = now;
            rt.LastTotalQueries = result.TotalQueries;
            rt.LastBlockedTotal = result.BlockedQueries;
            rt.LastQps = result.Qps;
            rt.LastBps = result.Bps;
            rt.LastDeltaQueries = result.DeltaQueries;
            rt.LastDeltaBlocked = result.DeltaBlocked;
            rt.ProtectionOn = result.ProtectionOn;
            rt.Online = true;
            rt.Error = "";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = TrimForStatus(ex.Message);
            var rt = GetRuntime(target.Name);
            rt.Online = false;
            rt.Error = result.Error;
        }

        return result;
    }

    private ServerRuntime GetRuntime(string name)
    {
        if (!_serverRuntime.TryGetValue(name, out var rt))
        {
            rt = new ServerRuntime();
            _serverRuntime[name] = rt;
        }
        return rt;
    }

    private void ApplyDualResults(List<ServerPollResult> results)
    {
        RefreshServerIdentityUi();

        var online = results.Where(r => r.Success).ToList();
        long total = online.Sum(r => r.TotalQueries);
        long blocked = online.Sum(r => r.BlockedQueries);
        long deltaQueries = online.Sum(r => r.DeltaQueries);
        long deltaBlocked = online.Sum(r => r.DeltaBlocked);
        double qps = online.Sum(r => r.Qps);
        double bps = online.Sum(r => r.Bps);
        double pct = total <= 0 ? 0 : blocked * 100.0 / total;
        double liveBlockPct = deltaQueries <= 0 ? 0 : Math.Clamp(deltaBlocked * 100.0 / deltaQueries, 0, 100);

        cardTotal.ValueText = total.ToString("N0");
        cardTotal.SubText = $"combined from {online.Count}/{results.Count} DNS boxes";
        cardBlocked.ValueText = blocked.ToString("N0");
        cardBlocked.SubText = $"combined blocked • Δ {deltaBlocked:N0}";
        cardBlockPct.ValueText = pct.ToString("0.0") + "%";
        cardBlockPct.SubText = $"live {liveBlockPct:0.0}% • overall {pct:0.0}%";
        cardLoad.ValueText = $"{qps:0.0} q/s";
        cardLoad.SubText = $"combined live load • {bps:0.0} block/s";

        var dns1 = results.FirstOrDefault(r => r.Target.Name.StartsWith("DNS1", StringComparison.OrdinalIgnoreCase));
        var dns2 = results.FirstOrDefault(r => r.Target.Name.StartsWith("DNS2", StringComparison.OrdinalIgnoreCase));
        if (chkPrimary.Checked)
            UpdateServerCard(cardClients, dns1, txtUrl.Text);
        else
            UpdateDisabledServerCard(cardClients, "disabled — " + ShortBaseUrl(txtUrl.Text));

        if (chkSecond.Checked)
            UpdateServerCard(cardLatency, dns2, txtUrl2.Text);
        else
            UpdateDisabledServerCard(cardLatency, "disabled — " + ShortBaseUrl(txtUrl2.Text));

        var active = online.OrderByDescending(r => r.Qps).FirstOrDefault();
        if (online.Count == 0)
        {
            cardProtection.ValueText = "OFFLINE";
            cardProtection.SubText = string.Join(" • ", results.Select(r => r.Target.Name + ": " + r.Error));
            cardProtection.AccentColor = Theme.Red;
        }
        else if (online.Count > 1 && online.All(r => r.DeltaQueries == 0))
        {
            cardProtection.ValueText = "IDLE";
            cardProtection.SubText = "both online • no new queries this poll";
            cardProtection.AccentColor = Theme.Blue;
        }
        else
        {
            cardProtection.ValueText = active?.Target.Name.Replace("DNS", "DNS ") ?? "ONLINE";
            cardProtection.SubText = online.Count == results.Count
                ? "highest live q/s this poll"
                : "one DNS server is offline";
            cardProtection.AccentColor = results.Any(r => !r.Success) ? Theme.Orange : Theme.Green;
        }

        _qpsPeak = Math.Max(10, Math.Max(_qpsPeak * 0.98, qps * 1.2));
        _bpsPeak = Math.Max(2, Math.Max(_bpsPeak * 0.98, bps * 1.2));
        Push(_qpsHistory, qps, 72);
        Push(_blockPctHistory, liveBlockPct, 72);

        graphQps.Values = _qpsHistory;
        graphQps.LeftText = $"combined now {qps:0.0} q/s";
        graphQps.RightText = $"peak {_qpsPeak:0.0}";

        graphBlockPct.Values = _blockPctHistory;
        double blockAvg = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Average();
        double blockPeak = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Max();
        double blockLow = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Min();
        graphBlockPct.LeftText = $"live {liveBlockPct:0.0}%  avg {blockAvg:0.0}%  overall {pct:0.0}%";
        graphBlockPct.RightText = $"low {blockLow:0.0}%  peak {blockPeak:0.0}%";

        meterQps.Maximum = _qpsPeak;
        meterQps.Value = qps;
        meterQps.BottomText = $"combined peak {_qpsPeak:0.0} q/s";

        meterBps.Maximum = _bpsPeak;
        meterBps.Value = bps;
        meterBps.BottomText = $"combined peak {_bpsPeak:0.0} b/s";

        var allRows = online.SelectMany(r => r.Rows).ToList();
        UpdateGrid(allRows);
        UpdateBars(online, allRows);
    }

    private void UpdateDisabledServerCard(StatCard card, string message)
    {
        card.ValueText = "DISABLED";
        card.SubText = message;
        card.AccentColor = Theme.Muted;
    }

    private void UpdateServerCard(StatCard card, ServerPollResult? result, string fallback)
    {
        string fallbackUrl = ShortBaseUrl(fallback);
        if (result == null)
        {
            card.ValueText = "OFF";
            card.SubText = fallbackUrl;
            card.AccentColor = Theme.Red;
            return;
        }

        string serverUrl = ShortBaseUrl(result.Target.BaseUrl);
        card.ValueText = result.Success ? $"{result.Qps:0.0} q/s" : "OFFLINE";
        card.SubText = result.Success
            ? $"{serverUrl} • Δ {result.DeltaQueries:N0} • {result.OverallBlockPct:0.0}% block"
            : $"{serverUrl} • {result.Error}";
        card.AccentColor = result.Success ? result.Target.Accent : Theme.Red;
    }

    private void UpdateGrid(List<QueryRow> rows)
    {
        var ordered = rows
            .OrderByDescending(r => r.TimeLocal)
            .Take((int)nudLimit.Value * 2)
            .ToList();

        var signature = string.Join("|", ordered.Take(40).Select(r => $"{r.SourceServer},{r.TimeLocal},{r.Client},{r.Domain},{r.Type},{r.Reason},{r.Status},{r.Elapsed}"));
        if (signature == _lastGridSignature)
            return;

        grid.SuspendLayout();
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        foreach (var r in ordered)
            _rows.Add(r);
        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
        grid.ResumeLayout();
        _lastGridSignature = signature;
    }

    private void UpdateBars(List<ServerPollResult> online, List<QueryRow> rows)
    {
        var clientStats = CombineBarItems(online.SelectMany(r => r.TopClients), 12);
        if (clientStats.Count == 0)
        {
            clientStats = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Client) ? "unknown" : r.Client)
                .Select(g => new BarItem { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList();
        }
        barsClients.Items = AddShareDetails(clientStats, "queries");

        var blockedStats = CombineBarItems(online.SelectMany(r => r.TopBlocked), 12);
        if (blockedStats.Count == 0)
        {
            blockedStats = rows
                .Where(r => r.Blocked)
                .GroupBy(r => CleanBlockedDomain(r.Domain, r.Reason, r.Status))
                .Select(g => new BarItem { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList();
        }
        if (blockedStats.Count == 0)
            blockedStats = CombineBarItems(online.SelectMany(r => r.TopQueried), 12);

        barsBlocked.Items = AddShareDetails(blockedStats, blockedStats.Count == 0 ? "waiting" : "blocked");
    }

    private static List<BarItem> CombineBarItems(IEnumerable<BarItem> items, int take)
    {
        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Label) && x.Value > 0)
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BarItem { Label = g.Key, Value = g.Sum(x => x.Value) })
            .OrderByDescending(x => x.Value)
            .Take(take)
            .ToList();
    }

    private static long GetBlockedTotal(JsonElement root)
    {
        return GetLong(root, "num_blocked_filtering")
            + GetLong(root, "num_replaced_safebrowsing")
            + GetLong(root, "num_replaced_safesearch")
            + GetLong(root, "num_replaced_parental");
    }

    private List<QueryRow> ExtractRows(JsonElement root, string serverName)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<QueryRow>();

        var rows = new List<QueryRow>();
        foreach (var item in data.EnumerateArray())
            rows.Add(ParseQueryLogRow(item, serverName));
        return rows;
    }

    private void ApplyStatus(JsonElement root)
    {
        bool on = GetFlexibleBool(root, "protection_enabled");
        cardProtection.ValueText = on ? "ON" : "OFF";
        cardProtection.SubText = NormalizeBaseUrl(_settings.BaseUrl).Replace("http://", "").Replace("https://", "");
        cardProtection.AccentColor = on ? Theme.Green : Theme.Red;
    }

    private void ApplyStats(JsonElement root)
    {
        long total = GetLong(root, "num_dns_queries");
        long blocked = GetLong(root, "num_blocked_filtering")
            + GetLong(root, "num_replaced_safebrowsing")
            + GetLong(root, "num_replaced_safesearch")
            + GetLong(root, "num_replaced_parental");

        double pct = total <= 0 ? 0 : blocked * 100.0 / total;
        cardTotal.ValueText = total.ToString("N0");
        cardBlocked.ValueText = blocked.ToString("N0");
        cardBlockPct.ValueText = pct.ToString("0.0") + "%";
        cardBlockPct.SubText = $"overall • {blocked:N0} blocked";

        _statsTopClients = ExtractBarItems(root, "top_clients", 12);
        _statsTopBlocked = ExtractBarItems(root, "top_blocked_domains", 12);
        _statsTopQueried = ExtractBarItems(root, "top_queried_domains", 12);

        if (_statsTopClients.Count > 0)
            barsClients.Items = AddShareDetails(_statsTopClients, "queries");
        if (_statsTopBlocked.Count > 0)
            barsBlocked.Items = AddShareDetails(_statsTopBlocked, "blocked");

        var now = DateTime.UtcNow;
        if (_lastStatTimeUtc != DateTime.MinValue)
        {
            double secs = Math.Max(0.1, (now - _lastStatTimeUtc).TotalSeconds);
            long deltaQueries = Math.Max(0, total - _lastTotalQueries);
            long deltaBlocked = Math.Max(0, blocked - _lastBlockedTotal);
            double qps = deltaQueries / secs;
            double bps = deltaBlocked / secs;
            double liveBlockPct = deltaQueries <= 0 ? 0 : Math.Clamp(deltaBlocked * 100.0 / deltaQueries, 0, 100);

            _qpsPeak = Math.Max(10, Math.Max(_qpsPeak * 0.98, qps * 1.2));
            _bpsPeak = Math.Max(2, Math.Max(_bpsPeak * 0.98, bps * 1.2));

            Push(_qpsHistory, qps, 72);
            Push(_blockPctHistory, liveBlockPct, 72);

            cardLoad.ValueText = $"{qps:0.0} q/s";
            cardLoad.SubText = "queries per second";

            graphQps.Values = _qpsHistory;
            graphQps.LeftText = $"now {qps:0.0} q/s";
            graphQps.RightText = $"peak {_qpsPeak:0.0}";

            graphBlockPct.Values = _blockPctHistory;
            double blockAvg = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Average();
            double blockPeak = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Max();
            double blockLow = _blockPctHistory.Count == 0 ? liveBlockPct : _blockPctHistory.Min();
            graphBlockPct.LeftText = $"live {liveBlockPct:0.0}%  avg {blockAvg:0.0}%  overall {pct:0.0}%";
            graphBlockPct.RightText = $"low {blockLow:0.0}%  peak {blockPeak:0.0}%";
            cardBlockPct.SubText = $"live {liveBlockPct:0.0}% • overall {pct:0.0}%";

            meterQps.Maximum = _qpsPeak;
            meterQps.Value = qps;
            meterQps.BottomText = $"peak {_qpsPeak:0.0} q/s";

            meterBps.Maximum = _bpsPeak;
            meterBps.Value = bps;
            meterBps.BottomText = $"peak {_bpsPeak:0.0} b/s";
        }
        else
        {
            cardLoad.ValueText = "0.0 q/s";
            meterQps.Value = 0;
            meterBps.Value = 0;
        }

        _lastStatTimeUtc = now;
        _lastTotalQueries = total;
        _lastBlockedTotal = blocked;
    }

    private void ApplyLog(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return;

        var rows = new List<QueryRow>();
        foreach (var item in data.EnumerateArray())
            rows.Add(ParseQueryLogRow(item));

        var signature = string.Join("|", rows.Take(30).Select(r => $"{r.TimeLocal},{r.Client},{r.Domain},{r.Type},{r.Reason},{r.Status},{r.Elapsed}"));
        if (signature != _lastGridSignature)
        {
            grid.SuspendLayout();
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var r in rows.OrderByDescending(r => r.TimeLocal))
                _rows.Add(r);
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
            grid.ResumeLayout();
            _lastGridSignature = signature;
        }

        int clients = rows.Select(r => r.Client).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        cardClients.ValueText = clients.ToString("N0");
        cardClients.SubText = $"sample {rows.Count:N0} rows";

        var ms = rows.Select(r => ParseMs(r.Elapsed)).Where(x => x >= 0).ToList();
        double avg = ms.Count == 0 ? 0 : ms.Average();
        cardLatency.ValueText = $"{avg:0.0} ms";
        cardLatency.SubText = "recent average";

        if (_statsTopClients.Count == 0)
        {
            barsClients.Items = AddShareDetails(rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Client) ? "unknown" : r.Client)
                .Select(g => new BarItem { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList(), "queries");
        }

        if (_statsTopBlocked.Count == 0)
        {
            var blockedFromLog = rows
                .Where(r => r.Blocked)
                .GroupBy(r => CleanBlockedDomain(r.Domain, r.Reason, r.Status))
                .Select(g => new BarItem { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList();

            if (blockedFromLog.Count > 0)
                barsBlocked.Items = AddShareDetails(blockedFromLog, "blocked");
            else if (_statsTopQueried.Count > 0)
                barsBlocked.Items = AddShareDetails(_statsTopQueried, "recent queries");
            else
                barsBlocked.Items = rows
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Reason) ? "waiting for blocked queries" : r.Reason)
                    .Select(g => new BarItem { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .Take(12)
                    .ToList();
        }
    }

    private QueryRow ParseQueryLogRow(JsonElement item) => ParseQueryLogRow(item, "DNS1 mini");

    private QueryRow ParseQueryLogRow(JsonElement item, string serverName)
    {
        var row = new QueryRow();
        row.SourceServer = serverName;
        row.TimeLocal = FormatTime(GetString(item, "time"));
        row.Client = GetString(item, "client");

        if (item.TryGetProperty("client_info", out var ci) && ci.ValueKind == JsonValueKind.Object)
        {
            var name = GetString(ci, "name");
            if (!string.IsNullOrWhiteSpace(name))
                row.Client = $"{name} ({row.Client})";
        }

        if (item.TryGetProperty("question", out var q) && q.ValueKind == JsonValueKind.Object)
        {
            row.Domain = FirstNonEmpty(GetString(q, "host"), GetString(q, "name"), GetString(q, "domain"));
            row.Type = FirstNonEmpty(GetString(q, "type"), GetString(q, "rr_type"));
        }
        else
        {
            row.Domain = FirstNonEmpty(GetString(item, "domain"), GetString(item, "host"), GetString(item, "name"));
            row.Type = FirstNonEmpty(GetString(item, "type"), GetString(item, "rr_type"));
        }

        if (string.IsNullOrWhiteSpace(row.Domain))
            row.Domain = FirstNonEmpty(GetString(item, "domain"), GetString(item, "host"), GetString(item, "name"));

        row.Status = FirstNonEmpty(GetString(item, "status"), GetString(item, "answer_status"));
        row.Reason = FirstNonEmpty(GetString(item, "reason"), GetString(item, "filter_id"), GetString(item, "rules"));
        row.Elapsed = FirstNonEmpty(GetString(item, "elapsedMs"), GetString(item, "elapsed_ms"), GetString(item, "elapsed"));
        if (!string.IsNullOrWhiteSpace(row.Elapsed) && !row.Elapsed.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            row.Elapsed += " ms";

        row.Upstream = GetString(item, "upstream");
        row.Blocked = IsBlocked(row.Status, row.Reason);
        return row;
    }

    private async Task<JsonDocument> GetJsonAsync(string baseUrl, string path, CancellationToken token)
    {
        string url = NormalizeBaseUrl(baseUrl) + path;
        using var response = await _http.GetAsync(url, token);
        string body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}. {TrimForStatus(body)}");
        return JsonDocument.Parse(body);
    }

    private void ConfigureHttpClient()
    {
        _http.DefaultRequestHeaders.Clear();
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_runtimePassword}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGWatch-REV16-AutoDnsSave/1.0");
    }

    private void OpenAdGuardUi(string baseUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo(NormalizeBaseUrl(baseUrl) + "/#logs") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not open browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplySettingsToUi()
    {
        _loadingSettingsToUi = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.PrimaryBaseUrl) && !string.IsNullOrWhiteSpace(_settings.BaseUrl))
                _settings.PrimaryBaseUrl = _settings.BaseUrl;
        if (string.IsNullOrWhiteSpace(_settings.PrimaryBaseUrl))
            _settings.PrimaryBaseUrl = "http://192.168.1.206:80";
        if (string.IsNullOrWhiteSpace(_settings.SecondaryBaseUrl))
            _settings.SecondaryBaseUrl = "http://192.168.1.207:3000";

        if (!_settings.PrimaryEnabled && !_settings.SecondaryEnabled)
            _settings.PrimaryEnabled = true;

        txtUrl.Text = _settings.PrimaryBaseUrl;
        txtUrl2.Text = _settings.SecondaryBaseUrl;

        _updatingDnsCheckboxes = true;
        chkPrimary.Checked = _settings.PrimaryEnabled;
        chkSecond.Checked = _settings.SecondaryEnabled;
        _updatingDnsCheckboxes = false;

        txtUser.Text = _settings.Username;
        _runtimePassword = PasswordVault.Unprotect(_settings.EncryptedPassword);
        if (string.IsNullOrEmpty(_runtimePassword) && !string.IsNullOrEmpty(_settings.Password))
            _runtimePassword = _settings.Password; // one-time legacy plain-text import
        txtPass.Text = _runtimePassword;
        nudPoll.Value = Math.Clamp(_settings.PollSeconds, 1, 30);
        nudLimit.Value = Math.Clamp(_settings.Limit, 50, 500);
            chkAuto.Checked = _settings.AutoStart;
            btnStop.Enabled = false;
        }
        finally
        {
            _loadingSettingsToUi = false;
        }

        RefreshServerIdentityUi();
    }

    private void OnDnsAddressChanged(string dnsName)
    {
        if (!_loadingSettingsToUi)
        {
            // The address changed, so do not compare the next stats poll against counters from the old box.
            foreach (string key in _serverRuntime.Keys.Where(k => k.StartsWith(dnsName, StringComparison.OrdinalIgnoreCase)).ToList())
                _serverRuntime.Remove(key);
            _lastGridSignature = string.Empty;
        }

        AutoSaveSettingsAndRefreshCards();
    }

    private void AutoSaveSettingsAndRefreshCards()
    {
        if (_loadingSettingsToUi || txtUrl == null || txtUrl2 == null || txtUser == null || txtPass == null || nudPoll == null || nudLimit == null || chkAuto == null)
            return;

        SaveSettingsFromUi();
        SaveSettings();
        RefreshServerIdentityUi();

        if (_timer.Enabled)
            _timer.Interval = _settings.PollSeconds * 1000;
    }

    private void RefreshServerIdentityUi()
    {
        if (cardClients == null || cardLatency == null || txtUrl == null || txtUrl2 == null)
            return;

        string dns1 = NormalizeBaseUrl(txtUrl.Text, "http://192.168.1.206:80");
        string dns2 = NormalizeBaseUrl(txtUrl2.Text, "http://192.168.1.207:3000");

        cardClients.Title = BuildServerName("DNS1", dns1).ToUpperInvariant();
        cardLatency.Title = BuildServerName("DNS2", dns2).ToUpperInvariant();

        if (!_timer.Enabled)
        {
            cardClients.SubText = ShortBaseUrl(dns1);
            cardLatency.SubText = ShortBaseUrl(dns2);
        }
        else
        {
            // While polling, the q/s cards include live data; this only keeps disabled cards current.
            if (!chkPrimary.Checked)
                cardClients.SubText = "disabled — " + ShortBaseUrl(dns1);
            if (!chkSecond.Checked)
                cardLatency.SubText = "disabled — " + ShortBaseUrl(dns2);
        }
    }

    private void SaveSettingsFromUi()
    {
        _settings.PrimaryBaseUrl = NormalizeBaseUrl(txtUrl.Text, "http://192.168.1.206:80");
        _settings.SecondaryBaseUrl = NormalizeBaseUrl(txtUrl2.Text, "http://192.168.1.207:3000");
        _settings.BaseUrl = _settings.PrimaryBaseUrl; // legacy compatibility
        _settings.PrimaryEnabled = chkPrimary.Checked;
        _settings.SecondaryEnabled = chkSecond.Checked;
        if (!_settings.PrimaryEnabled && !_settings.SecondaryEnabled)
            _settings.PrimaryEnabled = true;
        _settings.Username = txtUser.Text.Trim();
        _runtimePassword = txtPass.Text;
        _settings.EncryptedPassword = PasswordVault.Protect(_runtimePassword);
        _settings.Password = ""; // keep legacy plain-text field empty
        _settings.PollSeconds = (int)nudPoll.Value;
        _settings.Limit = (int)nudLimit.Value;
        _settings.AutoStart = chkAuto.Checked;
    }

    private void LoadSettings()
    {
        try
        {
            string legacyFile = Path.Combine(_legacySettingsDir, "settings.json");
            string fileToRead = File.Exists(SettingsFile) ? SettingsFile : legacyFile;

            if (File.Exists(fileToRead))
            {
                string json = File.ReadAllText(fileToRead);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                // If this was the old REV8B settings file, it will be migrated after the UI password box is populated.
                // Any legacy plain-text password is imported one time and then re-saved encrypted.
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static Label MakeTightLabel(string s) => new()
    {
        Text = s,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Theme.Muted,
        Padding = Padding.Empty,
        AutoEllipsis = true,
        UseMnemonic = false
    };

    private static Label MakeLabel(string s) => new()
    {
        Text = s,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        ForeColor = Theme.Muted,
        Padding = new Padding(0, 0, 8, 0)
    };

    private static TextBox MakeTextBox() => new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(9, 15, 26),
        ForeColor = Theme.Text
    };

    private static NumericUpDown MakeNum(decimal min, decimal max, decimal val, decimal inc) => new()
    {
        Dock = DockStyle.Fill,
        Minimum = min,
        Maximum = max,
        Value = val,
        Increment = inc,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(9, 15, 26),
        ForeColor = Theme.Text
    };

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        BackColor = color,
        ForeColor = Color.Black,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        Margin = new Padding(4)
    };

    private static void Push(List<double> list, double value, int max)
    {
        list.Add(value);
        while (list.Count > max) list.RemoveAt(0);
    }


    private static List<BarItem> ExtractBarItems(JsonElement root, string propertyName, int take)
    {
        if (!root.TryGetProperty(propertyName, out var source))
            return new List<BarItem>();

        var result = new List<BarItem>();

        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in source.EnumerateObject())
            {
                double value = GetFlexibleNumber(prop.Value);
                if (!string.IsNullOrWhiteSpace(prop.Name) && value > 0)
                    result.Add(new BarItem { Label = prop.Name, Value = value });
            }
        }
        else if (source.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in source.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    string label = FirstNonEmpty(
                        GetString(item, "domain"), GetString(item, "host"), GetString(item, "name"),
                        GetString(item, "client"), GetString(item, "ip"), GetString(item, "addr"));
                    double value = FirstPositive(
                        GetFlexibleNumber(item, "count"), GetFlexibleNumber(item, "queries"),
                        GetFlexibleNumber(item, "value"), GetFlexibleNumber(item, "num"));
                    if (!string.IsNullOrWhiteSpace(label) && value > 0)
                        result.Add(new BarItem { Label = label, Value = value });
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    string label = item.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(label))
                        result.Add(new BarItem { Label = label, Value = 1 });
                }
            }
        }

        return result
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BarItem { Label = g.Key, Value = g.Sum(x => x.Value) })
            .OrderByDescending(x => x.Value)
            .Take(take)
            .ToList();
    }

    private static List<BarItem> AddShareDetails(List<BarItem> source, string word)
    {
        double total = Math.Max(1, source.Sum(x => x.Value));
        return source.Select(x => new BarItem
        {
            Label = x.Label,
            Value = x.Value,
            Detail = $"{x.Value:0} {word} • {x.Value * 100.0 / total:0.0}%"
        }).ToList();
    }

    private static double FirstPositive(params double[] values) => values.FirstOrDefault(v => v > 0);

    private static double GetFlexibleNumber(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        return GetFlexibleNumber(v);
    }

    private static double GetFlexibleNumber(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out n)) return n;
        if (v.ValueKind == JsonValueKind.Object)
        {
            foreach (string key in new[] { "count", "queries", "value", "num" })
            {
                double nested = GetFlexibleNumber(v, key);
                if (nested > 0) return nested;
            }
        }
        return 0;
    }

    private static string CleanBlockedDomain(string domain, string reason, string status)
    {
        if (!string.IsNullOrWhiteSpace(domain)) return domain;
        if (!string.IsNullOrWhiteSpace(reason)) return reason;
        if (!string.IsNullOrWhiteSpace(status)) return status;
        return "unknown";
    }

    private static double ParseMs(string s)
    {
        s = s.Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(s, out var n) ? n : -1;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string FormatTime(string value)
    {
        if (DateTimeOffset.TryParse(value, out var dto))
            return dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return value;
    }

    private static string GetString(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => v.GetArrayLength() == 0 ? "" : v.ToString(),
            _ => v.ToString()
        };
    }

    private static long GetLong(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
        return long.TryParse(v.ToString(), out n) ? n : 0;
    }

    private static bool GetFlexibleBool(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return false;
        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        return v.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) || v.ToString() == "1";
    }

    private static bool IsBlocked(string status, string reason)
    {
        string r = (reason ?? "").Trim();
        string s = (status ?? "").Trim();

        if (r.StartsWith("NotFiltered", StringComparison.OrdinalIgnoreCase))
            return false;

        if (s.Equals("NOERROR", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("NOERROR_NODATA", StringComparison.OrdinalIgnoreCase))
        {
            return r.StartsWith("Filtered", StringComparison.OrdinalIgnoreCase) ||
                   r.Contains("Blocked", StringComparison.OrdinalIgnoreCase);
        }

        return r.StartsWith("Filtered", StringComparison.OrdinalIgnoreCase) ||
               r.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
               r.Contains("SafeBrowsing", StringComparison.OrdinalIgnoreCase) ||
               r.Contains("Parental", StringComparison.OrdinalIgnoreCase) ||
               s.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               s.Contains("filtered", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildServerName(string prefix, string baseUrl)
    {
        string host = ShortHostOnly(baseUrl);
        return string.IsNullOrWhiteSpace(host) ? prefix : $"{prefix} {host}";
    }

    private static string ShortBaseUrl(string baseUrl)
    {
        try
        {
            string normalized = NormalizeBaseUrl(baseUrl);
            var uri = new Uri(normalized);
            string host = uri.Host;
            string port = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return host + port;
        }
        catch
        {
            return (baseUrl ?? string.Empty).Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase).Trim().TrimEnd('/');
        }
    }

    private static string ShortHostOnly(string baseUrl)
    {
        try
        {
            string normalized = NormalizeBaseUrl(baseUrl);
            var uri = new Uri(normalized);
            return uri.Host;
        }
        catch
        {
            string text = ShortBaseUrl(baseUrl);
            int colon = text.IndexOf(':');
            return colon > 0 ? text[..colon] : text;
        }
    }

    private static string NormalizeBaseUrl(string input) => NormalizeBaseUrl(input, "http://192.168.1.206:80");

    private static string NormalizeBaseUrl(string input, string fallback)
    {
        input = (input ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(input)) input = fallback;
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            input = "http://" + input;
        return input;
    }

    private static string TrimForStatus(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= 180 ? s : s[..180] + "...";
    }
}
