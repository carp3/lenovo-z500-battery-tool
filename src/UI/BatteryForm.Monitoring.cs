using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

partial class BatteryForm
{
    void BuildMonitoring(TabControl tabs)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White };
        var controls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(4),
                                             WrapContents = true };
        controls.Controls.Add(
            new Label { Text = "Plot readings:", AutoSize = true, Margin = new Padding(5, 9, 10, 3) });
        chart.MinimumSize = new Size(100, 100);
        chart.Dock = DockStyle.Fill;
        chart.BackColor = Color.White;
        chart.AntiAliasing = AntiAliasingStyles.All;
        chart.Legends.Add(new Legend { Docking = Docking.Bottom, Font = new Font("Segoe UI", 9),
                                       BorderColor = Color.Transparent });
        string[] areas = { "Cells", "Pack", "Current", "Power", "Temperature", "Charge" };
        string[] units = { "Cells V", "Pack V", "Amps", "Watts", "°C", "%" };
        for (int i = 0; i < areas.Length; i++)
        {
            var area = new ChartArea(areas[i]);
            area.AxisY.Title = units[i];
            area.AxisY.TitleFont = new Font("Segoe UI", 9);
            area.InnerPlotPosition = new ElementPosition(12, 8, 85, 72);
            area.AxisY.IsStartedFromZero = false;
            area.AxisY.LabelStyle.Format = "0.###";
            area.AxisX.LabelStyle.Format = "HH:mm:ss";
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(237, 242, 246);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(228, 235, 241);
            area.AxisX.LineColor = area.AxisY.LineColor = Color.FromArgb(190, 204, 216);
            area.BackColor = Color.FromArgb(250, 252, 254);
            area.CursorX.IsUserEnabled = true;
            area.CursorX.IsUserSelectionEnabled = true;
            area.AxisX.ScaleView.Zoomable = true;
            chart.ChartAreas.Add(area);
        }
        for (int i = 0; i < MetricNames.Length; i++)
        {
            string name = MetricNames[i];
            var series = chart.Series.Add(name);
            series.ChartArea = MetricAreas[i];
            series.ChartType = SeriesChartType.Line;
            series.BorderWidth = 2;
            series.Color = MetricColors[i];
            series.XValueType = ChartValueType.DateTime;
            series.ToolTip = "#SERIESNAME\n#VALX{HH:mm:ss}  •  #VALY{0.###}";
            var check = new CheckBox { Text = name, Checked = i < 5, AutoSize = true,
                                       ForeColor = MetricColors[i], Margin = new Padding(7, 9, 7, 3) };
            selectors[name] = check;
            controls.Controls.Add(check);
            check.CheckedChanged += (o, e) => LayoutPlots();
        }
        var reset = new Button { Text = "Reset zoom", AutoSize = true };
        reset.Click += (o, e) =>
        {
            foreach (var a in chart.ChartAreas)
                a.AxisX.ScaleView.ZoomReset(0);
        };
        controls.Controls.Add(reset);
        tips.SetToolTip(
            chart,
            "Choose any combination above. Different units use separate axes/panels. Hover a line for exact values; drag across a panel to zoom, then Reset zoom. Up to 600 samples (~1 hour) are kept in memory.");
        panel.Controls.Add(chart);
        panel.Controls.Add(controls);
        AddTab(tabs, "Live graphs", panel);
        LayoutPlots();
    }
    void LayoutPlots()
    {
        foreach (var name in MetricNames)
            chart.Series[name].Enabled = selectors[name].Checked;
        var active = chart.ChartAreas.Cast<ChartArea>()
                         .Where(a => chart.Series.Cast<Series>().Any(s => s.ChartArea == a.Name && s.Enabled))
                         .ToArray();
        foreach (var a in chart.ChartAreas)
            a.Visible = active.Contains(a);
        for (int i = 0; i < active.Length; i++)
        {
            active[i].Position = new ElementPosition(0, 2 + i * 88f / active.Length, 98, 88f / active.Length);
            active[i].AxisX.LabelStyle.Enabled = i == active.Length - 1;
        }
        chart.Titles.Clear();
        if (active.Length == 0)
            chart.Titles.Add("Select a reading above to show its history");
    }
    void UpdateDashboard(BatterySnapshot s)
    {
        cards["Pack voltage"].Text = s.Voltage.ToString("F3") + " V";
        cards["Current"].Text = s.Current.ToString("+0.000;-0.000;0.000") + " A";
        cards["Temperature"].Text = s.Temperature.ToString("F1") + " °C";
        cards["Charge estimate"].Text = s.Words["0D"] + " %";
        for (int i = 0; i < 4; i++)
            cards[MetricNames[i]].Text = s.Cells == null ? "—" : (s.Cells[i] / 1000.0).ToString("F3") + " V";
    }
    void Plot(BatterySnapshot s)
    {
        double[] values = { 0, 0, 0, 0, s.Voltage, s.Current, s.Power, s.Temperature, s.Words["0D"] };
        for (int i = 0; i < 4; i++)
            if (s.Cells != null)
                values[i] = s.Cells[i] / 1000.0;
        for (int i = 0; i < MetricNames.Length; i++)
        {
            var series = chart.Series[MetricNames[i]];
            int n = series.Points.AddXY(s.TimestampUtc.ToLocalTime().ToOADate(), values[i]);
            if (i < 4 && s.Cells == null)
                series.Points[n].IsEmpty = true;
            while (series.Points.Count > 600)
                series.Points.RemoveAt(0);
        }
    }
    void PlotGap()
    {
        foreach (var series in chart.Series)
        {
            int n = series.Points.AddXY(DateTime.Now.ToOADate(), 0);
            series.Points[n].IsEmpty = true;
            while (series.Points.Count > 600)
                series.Points.RemoveAt(0);
        }
    }
}
