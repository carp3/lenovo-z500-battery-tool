using System;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class BatteryForm
{
    internal void VerifyLayout(BatterySnapshot sample)
    {
        Show();
        Application.DoEvents();
        for (int i = 0; i < 20; i++)
        {
            sample.TimestampUtc = DateTime.UtcNow.AddSeconds((i - 20) * 6);
            RenderSnapshot(sample);
        }
        var tabs = Controls.OfType<TabControl>().Single();
        if (tabs.TabPages.Cast<TabPage>().Any(t => t.Text.Contains("Ubuntu")))
            throw new Exception("Historical tab still present");
        if (!BuildOptions.EnableExperimentalCalibration && (start.Visible || stop.Visible))
            throw new Exception("Calibration controls visible in default build");
        if (cards.Count != 8 || chart.Series.Count != 9)
            throw new Exception("Missing cards/series");
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        connection.Text = "DEMO — synthetic data • no hardware access";
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(dir, "ui-dashboard.png"));
        }
        tabs.SelectedIndex = 3;
        selectors["Current"].Checked = true;
        selectors["Temperature"].Checked = true;
        selectors["Power"].Checked = true;
        Application.DoEvents();
        connection.Text = "DEMO — synthetic data • no hardware access";
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(dir, "ui-graphs.png"));
        }
        tabs.SelectedIndex = 1;
        searches[registers].Text = "signed";
        Application.DoEvents();
        if (registers.Rows.Cast<DataGridViewRow>().Count(r => r.Visible) != 2)
            throw new Exception("Search did not filter signed current registers");
        if (!registers.Rows[3].Cells[0].ToolTipText.Contains("signed"))
            throw new Exception("Missing register explanation tooltip");
        RenderSnapshot(sample);
        if (registers.Rows.Cast<DataGridViewRow>().Count(r => r.Visible) != 2)
            throw new Exception("Search lost on sample refresh");
        searches[registers].Text = "";
        if (registers.Rows.Cast<DataGridViewRow>().Count(r => r.Visible) != 26)
            throw new Exception("Clear search failed");
        connection.Text = "DEMO — synthetic data • no hardware access";
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(dir, "ui-registers.png"));
        }
        foreach (var check in selectors.Values)
            check.Checked = false;
        if (chart.ChartAreas.Cast<ChartArea>().Any(a => a.Visible))
            throw new Exception("Unchecked plot remains visible");
        foreach (var check in selectors.Values)
            check.Checked = true;
        if (chart.ChartAreas.Cast<ChartArea>().Count(a => a.Visible) != 6)
            throw new Exception("Missing plot areas");
        sample.Cells = null;
        RenderSnapshot(sample);
        if (cards["Cell 1 (A)"].Text != "—" || !chart.Series[0].Points.Last().IsEmpty)
            throw new Exception("Missing cell data not cleared");
        if (BatteryDecoder.DecodeDate(23339) != "11 September 2025 (pack-reported)" ||
            !BatteryDecoder.DecodeDate(0).StartsWith("Invalid"))
            throw new Exception("Manufacture date decoding failed");
        if (!BatteryDecoder.DecodeRegister(17, sample).StartsWith("Unavailable") ||
            !BatteryDecoder.DecodeRegister(26, sample).Contains("optional PEC"))
            throw new Exception("SBS decoding failed");
        File.WriteAllText(
            Path.Combine(dir, "ui-check.txt"),
            "PASS: dashboard and graphs rendered; 8 live cards; 9 selectable series / 6 scales; historical tab removed; tooltips; search, clear and refresh persistence; all/none series selection; missing-cell gaps. Render uses saved data, no hardware commands.");
        Close();
    }
}
