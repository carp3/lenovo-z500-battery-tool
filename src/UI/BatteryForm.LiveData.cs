using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class BatteryForm
{
    void Row(string k, object v, string note)
    {
        overview.Rows.Add(k, v, note);
    }
    async Task RefreshData()
    {
        if (busy)
            return;
        busy = true;
        start.Enabled = stop.Enabled = save.Enabled = false;
        try
        {
            var s =
                await Task.Run(() =>
                               { using (var b = new LenovoAcpiBridge()) return BatterySnapshot.Capture(b); });
            latest = s;
            lastError = null;
            RenderSnapshot(s);
            if (logging)
                Log(s);
            start.Enabled = BuildOptions.EnableExperimentalCalibration && (s.Gbmd & 1) == 0;
            stop.Enabled = BuildOptions.EnableExperimentalCalibration && (s.Gbmd & 1) != 0;
            save.Enabled = true;
        }
        catch (Exception ex)
        {
            latest = null;
            lastError = ex.Message;
            headline.Text = "Live battery data unavailable";
            connection.Text = "READ FAILED — " + ex.Message;
            overview.Rows.Clear();
            registers.Rows.Clear();
            flags.Rows.Clear();
            raw.Text = "Current sample failed. No stale values displayed.\r\n" + ex;
            foreach (var label in cards.Values)
                label.Text = "—";
            PlotGap();
        }
        finally
        {
            busy = false;
        }
    }

    void RenderSnapshot(BatterySnapshot s)
    {
        headline.Text = String.Format(
            "{0:F3} V   |   {1:+0.000;-0.000;0.000} A   |   {2:+0.00;-0.00;0.00} W   |   {3:F1} °C",
            s.Voltage, s.Current, s.Power, s.Temperature);
        connection.Text = "LIVE • Lenovo ACPI → EC → SBS • " +
                          s.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") +
                          " • calibration " + ((s.Gbmd & 1) != 0 ? "ACTIVE" : "inactive");
        overview.Rows.Clear();
        string unit = s.EnergyUnits ? "Wh" : "Ah";
        Row("Charge estimate", s.Words["0D"] + " %", "SBS 0x0D; numerical estimate, not FULLY_CHARGED flag");
        Row("Remaining", s.Remaining.ToString("F2") + " " + unit, "SBS 0x0F; units from BatteryMode bit 15");
        Row("Full / design", s.Full.ToString("F2") + " / " + s.Design.ToString("F2") + " " + unit,
            "SBS 0x10 / 0x18");
        Row("Reported full/design ratio",
            s.Design > 0 ? (100 * s.Full / s.Design).ToString("F2") + " %" : "Unavailable",
            "Controller estimate; not independently verified wear");
        Row("Current / average",
            BatterySnapshot.Signed(s.Words["0A"]) + " / " + BatterySnapshot.Signed(s.Words["0B"]) + " mA",
            "Positive = charging; negative = discharging");
        Row("Requested charge", s.Words["15"] + " mV / " + s.Words["14"] + " mA",
            "Pack request; zero means no charge requested");
        Row("Cycle count", s.Words["17"], "Direct SBS, not hardcoded ACPI cycle zero");
        Row("Manufacturer / model",
            BatteryDecoder.BlockText(s.Blocks["20"]) + " / " + BatteryDecoder.BlockText(s.Blocks["21"]),
            "Pack strings may be cloned");
        if (s.Cells != null)
        {
            for (int i = 0; i < 4; i++)
                Row("Cell/group " + (char)('A' + i), (s.Cells[i] / 1000.0).ToString("F3") + " V",
                    "Manufacturer block 0x23; data order only");
            Row("Cell spread / sum", (s.Cells.Max() - s.Cells.Min()) + " mV / " + s.Cells.Sum() + " mV",
                "Plausibility checks passed; not safety thresholds");
        }
        else
            Row("Cell/group readings", "Unavailable", s.CellError);
        Row("Firmware mode", "0x" + s.Gbmd.ToString("X8"),
            "GBMD bit 0 = calibration mode; not learning completion");
        Row("AC adapter", SystemInformation.PowerStatus.PowerLineStatus, "Windows power status");
        Row("Windows charge/discharge rate", "Not used",
            "Known BIOS conversion error (655.35 W at zero current)");
        registers.Rows.Clear();
        for (int i = 0; i < Commands.Length; i++)
        {
            var key = Commands[i].ToString("X2");
            registers.Rows.Add("0x" + key + " " + Names[i], BatteryDecoder.DecodeRegister(Commands[i], s),
                               BatteryDecoder.RegisterHelp(Commands[i]),
                               "0x" + s.Words[key].ToString("X4") + " / " + s.Words[key]);
        }
        foreach (var pair in s.Blocks)
            registers.Rows.Add("0x" + pair.Key + " block", BatteryDecoder.DecodeBlock(pair.Key, s),
                               BatteryDecoder.BlockHelp(pair.Key), pair.Value);
        flags.Rows.Clear();
        int status = s.Words["16"];
        for (int i = 0; i < BatteryDecoder.StatusFlagNames.Length; i++)
            flags.Rows.Add(BatteryDecoder.StatusFlagNames[i],
                           (status & (1 << (15 - i))) != 0 ? "SET" : "clear",
                           BatteryDecoder.StatusHelp(15 - i));
        flags.Rows.Add("Error nibble", status & 15, "SBS BatteryStatus bits 0–3");
        string[] symbols = { "CDMB", "BTSM", "FCGM",         "MBBD", "SBBD", "BTCM", "unused",
                             "BTIL", "BTPF", "BTCM inverse", "SBIL", "BTOV", "SBOV", "SBPF" };
        for (int i = 0; i < symbols.Length; i++)
            flags.Rows.Add("GBMD " + symbols[i], (s.Gbmd & (1u << i)) != 0 ? "SET" : "clear",
                           "Firmware symbol, bit " + i + "; do not infer undocumented meaning");
        raw.Text = JsonFormatter.Format(new JavaScriptSerializer().Serialize(s));
        UpdateDashboard(s);
        Plot(s);
        foreach (var g in new[] { overview, registers, flags })
            Filter(g);
    }
}
