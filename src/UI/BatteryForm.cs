using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

partial class BatteryForm : Form
{
    Label headline = new Label(), connection = new Label(), notice = new Label();
    readonly Color accent = Color.FromArgb(24, 112, 155);
    ToolTip tips = new ToolTip { AutoPopDelay = 18000, InitialDelay = 250, ReshowDelay = 100 };
    Dictionary<string, Label> cards = new Dictionary<string, Label>();
    Dictionary<DataGridView, TextBox> searches = new Dictionary<DataGridView, TextBox>();
    Dictionary<string, CheckBox> selectors = new Dictionary<string, CheckBox>();
    static readonly string[] MetricNames = { "Cell 1 (A)", "Cell 2 (B)",   "Cell 3 (C)",
                                             "Cell 4 (D)", "Pack voltage", "Current",
                                             "Power",      "Temperature",  "Charge" };
    static readonly string[] MetricAreas = { "Cells",   "Cells", "Cells",       "Cells", "Pack",
                                             "Current", "Power", "Temperature", "Charge" };
    static readonly Color[] MetricColors = { Color.FromArgb(25, 145, 135), Color.FromArgb(74, 111, 220),
                                             Color.FromArgb(175, 94, 204), Color.FromArgb(224, 151, 30),
                                             Color.FromArgb(25, 105, 165), Color.FromArgb(30, 145, 110),
                                             Color.FromArgb(180, 105, 40), Color.FromArgb(210, 80, 95),
                                             Color.FromArgb(70, 140, 190) };

    DataGridView overview = Grid(), registers = Grid(), flags = Grid();
    TextBox raw = new TextBox();
    Chart chart = new Chart();
    Button start = new Button(), stop = new Button(), save = new Button();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    bool busy;
    BatterySnapshot latest;
    string lastError = "Waiting for first sample";
    bool logging;
    static readonly string[] Names = { "BatteryMode",
                                       "Temperature",
                                       "Voltage",
                                       "Current",
                                       "AverageCurrent",
                                       "MaxError",
                                       "RelativeStateOfCharge",
                                       "AbsoluteStateOfCharge",
                                       "RemainingCapacity",
                                       "FullChargeCapacity",
                                       "RunTimeToEmpty",
                                       "AverageTimeToEmpty",
                                       "AverageTimeToFull",
                                       "ChargingCurrent",
                                       "ChargingVoltage",
                                       "BatteryStatus",
                                       "CycleCount",
                                       "DesignCapacity",
                                       "DesignVoltage",
                                       "SpecificationInfo",
                                       "ManufactureDate",
                                       "SerialNumber" };
    static readonly byte[] Commands = { 3,  8,  9,  10, 11, 12, 13, 14, 15, 16, 17,
                                        18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 };

    static DataGridView Grid()
    {
        var g = new DataGridView {
            Dock = DockStyle.Fill,         ReadOnly = true,
            AllowUserToAddRows = false,    AllowUserToDeleteRows = false,
            RowHeadersVisible = false,     AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White, SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        g.Columns.Add("field", "Reading");
        g.Columns.Add("value", "Value");
        g.Columns.Add("source", "Source / interpretation");
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = Color.FromArgb(231, 236, 241);
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 240, 246);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(38, 65, 88);
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        g.ColumnHeadersHeight = 38;
        g.RowTemplate.Height = 34;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(215, 234, 245);
        g.DefaultCellStyle.SelectionForeColor = Color.FromArgb(22, 43, 63);
        g.Columns[0].FillWeight = 30;
        g.Columns[1].FillWeight = 26;
        g.Columns[2].FillWeight = 44;
        g.ShowCellToolTips = true;
        return g;
    }
    internal BatteryForm(bool preview = false)
    {
        Text = "Z500 Battery Tool — experimental monitor";
        Width = 1220;
        Height = 900;
        MinimumSize = new Size(950, 700);
        BackColor = Color.FromArgb(242, 246, 250);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        var header = new Panel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(18),
                                 BackColor = Color.FromArgb(23, 47, 69) };
        headline.SetBounds(18, 12, 1140, 32);
        headline.Font = new Font("Segoe UI", 17, FontStyle.Bold);
        headline.ForeColor = Color.White;
        headline.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        headline.Text = "Z500 • Battery dashboard";
        connection.SetBounds(20, 51, 1140, 23);
        connection.ForeColor = Color.FromArgb(166, 218, 236);
        connection.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        notice.SetBounds(20, 79, 1140, 24);
        notice.ForeColor = Color.FromArgb(204, 218, 230);
        notice.Text =
            "Direct battery readings • cell labels follow data order • hover over readings for details";
        header.Controls.AddRange(new Control[] { headline, connection, notice });
        Controls.Add(header);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(20, 9) };
        var dashboard = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var cardTable = new TableLayoutPanel { Dock = DockStyle.Top, Height = 218, ColumnCount = 4,
                                               RowCount = 2, Padding = new Padding(0, 0, 0, 10) };
        for (int i = 0; i < 4; i++)
            cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int i = 0; i < 2; i++)
            cardTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        string[] titles = { "Pack voltage", "Current",    "Temperature", "Charge estimate",
                            "Cell 1 (A)",   "Cell 2 (B)", "Cell 3 (C)",  "Cell 4 (D)" };
        for (int i = 0; i < titles.Length; i++)
        {
            var tile = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5),
                                   Padding = new Padding(14, 8, 8, 8) };
            var label =
                new Label { Text = titles[i], Dock = DockStyle.Top, Height = 24,
                            ForeColor = Color.FromArgb(79, 99, 116), Font = new Font("Segoe UI", 10) };
            var value = new Label { Text = "—", Dock = DockStyle.Fill,
                                    ForeColor = i >= 4 ? MetricColors[i - 4] : accent,
                                    Font = new Font("Segoe UI", 25, FontStyle.Bold),
                                    TextAlign = ContentAlignment.MiddleLeft };
            tile.Controls.Add(value);
            tile.Controls.Add(label);
            cardTable.Controls.Add(tile, i % 4, i / 4);
            cards[titles[i]] = value;
            tips.SetToolTip(
                value,
                i >= 4
                    ? "Live voltage of cell/group " + (i - 3) +
                          ". A–D are manufacturer-block data order, not confirmed physical numbering. Hover over the list for source details."
                : titles[i] == "Current"
                    ? "Direct SBS current: positive charges the pack; negative discharges it. This avoids the BIOS power conversion error."
                : titles[i] == "Temperature"
                    ? "Pack controller temperature sensor; not individual cell temperatures."
                : titles[i] == "Charge estimate"
                    ? "Controller's numerical charge estimate. 100% does not imply the FULLY_CHARGED status flag is set."
                    : "Direct SBS battery-terminal voltage, in volts.");
        }
        dashboard.Controls.Add(Searchable(overview));
        dashboard.Controls.Add(cardTable);
        registers.Columns[1].HeaderText = "Decoded value";
        registers.Columns.Add("raw", "Raw (hex / decimal)");
        registers.Columns[3].DisplayIndex = 2;
        registers.Columns[2].DisplayIndex = 3;
        registers.Columns[0].FillWeight = 22;
        registers.Columns[1].FillWeight = 29;
        registers.Columns[2].FillWeight = 30;
        registers.Columns[3].FillWeight = 19;
        registers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        registers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        AddTab(tabs, "Dashboard", dashboard);
        AddTab(tabs, "SBS registers", Searchable(registers));
        AddTab(tabs, "Status flags", Searchable(flags));
        BuildMonitoring(tabs);
        raw.Multiline = true;
        raw.ScrollBars = ScrollBars.Both;
        raw.WordWrap = false;
        raw.Dock = DockStyle.Fill;
        raw.ReadOnly = true;
        raw.Font = new Font("Consolas", 10);
        AddTab(tabs, "Raw live snapshot", raw);
        var info = new TextBox {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
            Text =
                "THIS LAPTOP ONLY: Lenovo 20202 / IdeaPad Z500\r\n\r\nTransport: installed Lenovo AcpiVpc.sys 15.11.29.70 via \\\\.\\EnergyDrv.\r\nGBMD / SBMC: IOCTL 0x831020F8. Read uses byte 0xFF. Calibration uses 1 / 0, disabled by default at build time.\r\nMHPF: IOCTL 0x83102114, 37-byte buffer; administrator required.\r\nFirmware and driver SHA-256 must match the inspected originals.\r\n\r\nCells A–D are block 0x23 payload offsets 4–11, little-endian mV. Physical order is unknown. Sum/header checks reject invalid cells. No controller identity is inferred from copied pack strings.\r\n\r\nCalibration bit is a mode indicator. An inactive bit alone does not prove a successful learning cycle. The app never disables Windows critical-battery protections or changes sleep settings. Firmware calibration can continue after this app closes.\r\n\r\nClose other battery/EC tools while using this monitor. Unknown firmware flag names retain their original symbols. No unseal, protection clear, force-charge, raw port, or arbitrary register writes are exposed.\r\n\r\nRead failures invalidate the current display; no failed sample is logged. CSV logging is opt-in and written alongside this app. Snapshots are saved there too. They contain pack identity and serial data; review before sharing."
        };
        AddTab(tabs, "Interface & limits", info);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
        start.Visible = stop.Visible = BuildOptions.EnableExperimentalCalibration;
        start.Text = "Start calibration…";
        stop.Text = "Exit calibration…";
        save.Text = "Save snapshot";
        var log = new Button { Text = "Start CSV logging", AutoSize = true };
        var refresh = new Button { Text = "Refresh", AutoSize = true };
        foreach (var button in new[] { start, stop, save, log, refresh })
        {
            button.AutoSize = true;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(199, 215, 228);
            button.BackColor = Color.White;
            button.Padding = new Padding(8, 3, 8, 3);
            buttons.Controls.Add(button);
        }
        buttons.Height = 58;
        start.BackColor = Color.FromArgb(223, 240, 248);
        tips.SetToolTip(
            start,
            "Start the firmware gauge cycle after confirmation, AC and alarm checks. Calibration can continue after this app closes.");
        tips.SetToolTip(
            stop, "Exit calibration mode. This does not clear pack alarms or prove learning completed.");
        start.Click += async (o, e) => await Calibrate(true);
        stop.Click += async (o, e) => await Calibrate(false);
        refresh.Click += async (o, e) => await RefreshData();
        save.Click += (o, e) => Save();
        log.Click += (o, e) =>
        {
            logging = !logging;
            log.Text = logging ? "Stop CSV logging" : "Start CSV logging";
        };
        Controls.Add(tabs);
        Controls.Add(buttons);
        tabs.BringToFront();
        timer.Interval = 6000;
        timer.Tick += async (o, e) => await RefreshData();
        Shown += async (o, e) =>
        {
            if (!preview)
            {
                await RefreshData();
                timer.Start();
            }
        };
        FormClosed += (o, e) => timer.Stop();
    }
    static void AddTab(TabControl tabs, string title, Control c)
    {
        var tab = new TabPage(title);
        tab.Controls.Add(c);
        tabs.TabPages.Add(tab);
    }
    async Task Calibrate(bool begin)
    {
        if (!BuildOptions.EnableExperimentalCalibration)
            return;
        if (busy)
            return;
        if (MessageBox.Show(
                begin
                    ? "Start a firmware gauge calibration? Connect AC, close other work, and keep the laptop awake. This can take hours. Windows critical-battery protection remains enabled. The current sample and charge-stop alarms will be checked before sending SBMC 1."
                    : "Exit firmware calibration mode with SBMC 0? This does not clear battery protections or prove calibration completed.",
                "Z500 calibration", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;
        busy = true;
        start.Enabled = stop.Enabled = false;
        try
        {
            await Task.Run(
                () =>
                {
                    using (var b = new LenovoAcpiBridge())
                    {
                        if (SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online)
                            throw new Exception("Connect AC power first.");
                        var s = begin ? BatterySnapshot.Capture(b) : new BatterySnapshot { Gbmd = b.Mode() };
                        if (begin && (s.Gbmd & 1) != 0)
                            throw new Exception("Calibration already active; no command sent.");
                        if (!begin && (s.Gbmd & 1) == 0)
                            throw new Exception("Calibration already inactive; no command sent.");
                        if (begin && (s.Words["16"] & 0xD800) != 0)
                            throw new Exception(
                                "Pack reports charge-stop/temperature/discharge-stop alarms. No calibration started. See Status flags.");
                        if (SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online)
                            throw new Exception("AC disconnected; no command sent.");
                        b.Calibration(begin);
                        Thread.Sleep(1000);
                        uint mode = b.Mode();
                        File.AppendAllText(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calibration-actions.log"),
                            DateTime.UtcNow.ToString("o") + " SBMC " + (begin ? 1 : 0) + " GBMD before=0x" +
                                s.Gbmd.ToString("X8") + " after=0x" + mode.ToString("X8") +
                                Environment.NewLine);
                        if (((mode & 1) != 0) != begin)
                            throw new Exception(
                                "Command sent but firmware mode verification failed. Inspect live state; do not repeat blindly.");
                    }
                });
            MessageBox.Show(
                begin
                    ? "Firmware reports calibration ACTIVE. Keep AC connected. Closing the app does not cancel the firmware cycle."
                    : "Firmware reports calibration INACTIVE. This verifies mode exit, not successful learning.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            busy = false;
        }
        await RefreshData();
    }
    void Save()
    {
        try
        {
            if (latest == null)
                throw new Exception(lastError ?? "No valid live snapshot");
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                    "snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(p, new JavaScriptSerializer().Serialize(latest));
            MessageBox.Show(p, "Saved");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
    void Log(BatterySnapshot s)
    {
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor.csv");
        if (!File.Exists(p))
            File.AppendAllText(
                p,
                "utc,voltage_V,current_A,power_W,temp_C,remaining,full,design,capacity_unit,percent,status,gbmd,cellA_mV,cellB_mV,cellC_mV,cellD_mV\r\n");
        var c = System.Globalization.CultureInfo.InvariantCulture;
        var values = new List<string> { s.TimestampUtc.ToString("o"),
                                        s.Voltage.ToString(c),
                                        s.Current.ToString(c),
                                        s.Power.ToString(c),
                                        s.Temperature.ToString(c),
                                        s.Remaining.ToString(c),
                                        s.Full.ToString(c),
                                        s.Design.ToString(c),
                                        s.EnergyUnits ? "Wh" : "Ah",
                                        s.Words["0D"].ToString(),
                                        "0x" + s.Words["16"].ToString("X4"),
                                        "0x" + s.Gbmd.ToString("X8") };
        for (int i = 0; i < 4; i++)
            values.Add(s.Cells == null ? "" : s.Cells[i].ToString());
        File.AppendAllText(p, string.Join(",", values) + "\r\n");
    }
}
