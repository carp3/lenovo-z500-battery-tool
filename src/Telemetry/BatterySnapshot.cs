using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

sealed class BatterySnapshot
{
    public DateTime TimestampUtc;
    public uint Gbmd;
    public Dictionary<string, int> Words = new Dictionary<string, int>();
    public Dictionary<string, string> Blocks = new Dictionary<string, string>();
    public int[] Cells;
    public string CellError;
    public double Voltage, Current, Power, Temperature, Remaining, Full, Design;
    public bool EnergyUnits;
    public static int Signed(int raw)
    {
        return (short)raw;
    }
    public static int[] DecodeCells(byte[] b, int voltage)
    {
        if (b.Length < 14 || b[0] != 0x42 || b[1] != 1 || b[2] != 1 || b[3] != 1)
            throw new Exception("Cell block header differs from inspected pack");
        int[] cells = Enumerable.Range(0, 4).Select(i => (int)BitConverter.ToUInt16(b, 4 + 2 * i)).ToArray();
        if (cells.Any(v => v < 2000 || v > 4500) || Math.Abs(cells.Sum() - voltage) > 150)
            throw new Exception("Cell block failed plausibility/sum checks (not safety limits)");
        return cells;
    }
    internal static BatterySnapshot Capture(LenovoAcpiBridge b)
    {
        var s = new BatterySnapshot();
        s.TimestampUtc = DateTime.UtcNow;
        s.Gbmd = b.Mode();
        byte[] commands = { 9,  10, 11, 8,  3,  12, 13, 14, 15, 16, 17,
                            18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 };
        foreach (byte c in commands)
            s.Words[c.ToString("X2")] = b.Word(c);
        if (s.Words["09"] < 8000 || s.Words["09"] > 20000)
            throw new Exception("Pack voltage failed Z500 plausibility check");
        foreach (byte c in new byte[] { 0x20, 0x21, 0x22, 0x23 })
        {
            var raw = b.Read(c, true);
            s.Blocks[c.ToString("X2")] = BitConverter.ToString(raw).Replace("-", "");
            if (c == 0x23)
            {
                try
                {
                    s.Cells = DecodeCells(raw, s.Words["09"]);
                }
                catch (Exception ex)
                {
                    s.CellError = ex.Message;
                }
            }
        }
        s.Voltage = s.Words["09"] / 1000.0;
        s.Current = Signed(s.Words["0A"]) / 1000.0;
        s.Power = s.Voltage * s.Current;
        s.Temperature = s.Words["08"] / 10.0 - 273.15;
        s.EnergyUnits = (s.Words["03"] & 0x8000) != 0;
        double scale = s.EnergyUnits ? 0.01 : 0.001;
        s.Remaining = s.Words["0F"] * scale;
        s.Full = s.Words["10"] * scale;
        s.Design = s.Words["18"] * scale;
        return s;
    }
}
