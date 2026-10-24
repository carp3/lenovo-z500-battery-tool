using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class BatteryForm
{
    Control Searchable(DataGridView grid)
    {
        var container = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var bar = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12) };
        var label = new Label { Text = "Search readings", Dock = DockStyle.Left, Width = 120,
                                TextAlign = ContentAlignment.MiddleLeft };
        var input = new TextBox { Dock = DockStyle.Fill };
        searches[grid] = input;
        bar.Controls.Add(input);
        bar.Controls.Add(label);
        container.Controls.Add(grid);
        container.Controls.Add(bar);
        input.TextChanged += (o, e) => Filter(grid);
        tips.SetToolTip(
            input,
            "Filter by name, register address, value, or explanation. Clear the field to show all readings.");
        return container;
    }
    void Filter(DataGridView grid)
    {
        TextBox input;
        if (!searches.TryGetValue(grid, out input))
            return;
        string query = input.Text.Trim();
        grid.CurrentCell = null;
        foreach (DataGridViewRow row in grid.Rows)
        {
            string name = Convert.ToString(row.Cells[0].Value), note = Convert.ToString(row.Cells[2].Value);
            if (name.StartsWith("GBMD "))
                note +=
                    ". " +
                    (name.Contains("CDMB")
                         ? "Bit 0 indicates firmware calibration mode. It is not a learning-cycle completion signal."
                         : "Original firmware symbol; expanded meaning has not been established on this Z500. Do not interpret the name as a fault diagnosis.");
            string tooltip = name + "\r\n" + note;
            foreach (DataGridViewCell cell in row.Cells)
                cell.ToolTipText = tooltip;
            string haystack =
                string.Join(" ", row.Cells.Cast<DataGridViewCell>().Select(c => Convert.ToString(c.Value))) +
                " " + note;
            row.Visible =
                query.Length == 0 || haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
