using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {

#if TESTS
        if (args.Contains("--self-test"))
        {
            try
            {
                byte[] sample = { 0x42, 1, 1, 1, 0x93, 0x0f, 0x89, 0x0f, 0x8f, 0x0f, 0x88, 0x0f, 5, 0x80 };
                var cells = BatterySnapshot.DecodeCells(sample, 15923);
                if (cells.Sum() != 15923 || cells.Max() - cells.Min() != 11 ||
                    BatterySnapshot.Signed(65535) != -1 || BatterySnapshot.Signed(32768) != -32768)
                    throw new Exception("Decode failed");
                bool rejected = false;
                try
                {
                    BatterySnapshot.DecodeCells(sample, 14000);
                }
                catch
                {
                    rejected = true;
                }
                if (!rejected)
                    throw new Exception("Bad sum accepted");
                File.WriteAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt"),
                    "PASS: recorded cell block, signed current boundaries, rejected invalid cell sum.");
                return 0;
            }
            catch
            {
                return 1;
            }
        }

#endif
        if (args.Contains("--probe"))
        {
            try
            {
                using (var b = new LenovoAcpiBridge())
                {
                    var s = BatterySnapshot.Capture(b);
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live-probe.json"),
                                      new JavaScriptSerializer().Serialize(s));
                }
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live-probe-error.txt"),
                                  ex.ToString());
                return 1;
            }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

#if TESTS
        if (args.Contains("--ui-check"))
        {
            try
            {
                var sample = new JavaScriptSerializer().Deserialize<BatterySnapshot>(
                    File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample.json")));
                using (var form = new BatteryForm(true)) form.VerifyLayout(sample);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-check-error.txt"),
                                  ex.ToString());
                return 1;
            }
        }

#endif
        bool created;
        using (var mutex = new Mutex(true, "Local\\Z500BatteryMonitor", out created))
        {
            if (!created)
            {
                MessageBox.Show(
                    "Z500 Battery is already running. Close it before starting another instance.");
                return 1;
            }
            Application.Run(new BatteryForm());
        }
        return 0;
    }
}
