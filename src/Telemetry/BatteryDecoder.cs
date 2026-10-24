using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class BatteryDecoder
{
    internal static readonly string[] StatusFlagNames = { "OVER_CHARGED_ALARM",
                                                          "TERMINATE_CHARGE_ALARM",
                                                          "Reserved 13",
                                                          "OVER_TEMP_ALARM",
                                                          "TERMINATE_DISCHARGE_ALARM",
                                                          "Reserved 10",
                                                          "REMAINING_CAPACITY_ALARM",
                                                          "REMAINING_TIME_ALARM",
                                                          "INITIALIZED",
                                                          "DISCHARGING / not accepting charge",
                                                          "FULLY_CHARGED",
                                                          "FULLY_DISCHARGED" };
    internal static string DecodeDate(int raw)
    {
        try
        {
            return new DateTime(1980 + (raw >> 9), (raw >> 5) & 15, raw & 31)
                       .ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture) +
                   " (pack-reported)";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Invalid / unavailable packed date";
        }
    }
    internal static string DecodeRegister(byte command, BatterySnapshot s)
    {
        int v = s.Words[command.ToString("X2")];
        int info = s.Words["1A"], vs = (info >> 8) & 15, ips = (info >> 12) & 15;
        double vscale = Math.Pow(10, vs), iscale = Math.Pow(10, ips);
        switch (command)
        {
        case 3:
            return ((v & 0x8000) != 0 ? "Capacity: 10 mWh" : "Capacity: mAh") + "; alarm broadcasts " +
                   ((v & 0x2000) != 0 ? "disabled" : "enabled") + "; charger broadcasts " +
                   ((v & 0x4000) != 0 ? "disabled" : "enabled") + "; conditioning " +
                   ((v & 128) != 0 ? "requested" : "not requested") + "; internal charger " +
                   ((v & 1) != 0 ? "supported" : "not supported") + " / " +
                   ((v & 256) != 0 ? "enabled" : "disabled") + "; primary-role control " +
                   ((v & 2) != 0 ? "supported" : "not supported") + " / " +
                   ((v & 512) != 0 ? "primary" : "secondary");
        case 8:
            return (v / 10.0 - 273.15).ToString("F2") + " °C";
        case 9:
        case 25:
            return vs > 3 ? "Unknown voltage scale" : (v * vscale / 1000).ToString("F3") + " V";
        case 10:
        case 11:
            return ips > 3 ? "Unknown current scale"
                           : (BatterySnapshot.Signed(v) * iscale).ToString("0.###") + " mA (" +
                                 (BatterySnapshot.Signed(v) > 0   ? "charging"
                                  : BatterySnapshot.Signed(v) < 0 ? "discharging"
                                                                  : "no net current") +
                                 ")";
        case 12:
        case 13:
        case 14:
            return v + " %";
        case 15:
        case 16:
        case 24:
            return ips > 3 ? "Unknown capacity scale"
                           : (v * iscale * ((s.Words["03"] & 0x8000) != 0 ? 0.01 : 0.001)).ToString("F3") +
                                 ((s.Words["03"] & 0x8000) != 0 ? " Wh" : " Ah");
        case 17:
        case 18:
        case 19:
            return v == 65535 ? "Unavailable / not applicable"
                              : (v / 60) + " h " + (v % 60) + " min (" + v + " min)";
        case 20:
            return v == 65535 ? "Maximum current requested" : v + " mA requested";
        case 21:
            return v == 65535 ? "Maximum voltage requested" : (v / 1000.0).ToString("F3") + " V requested";
        case 22:
            var states = new List<string>();
            for (int i = 0; i < StatusFlagNames.Length; i++)
                if ((v & (1 << (15 - i))) != 0)
                    states.Add(StatusFlagNames[i]);
            string[] errors = { "OK",
                                "Busy",
                                "Reserved command",
                                "Unsupported command",
                                "Access denied",
                                "Overflow / underflow",
                                "Bad size",
                                "Unknown error" };
            int error = v & 15;
            states.Add("Error: " + (error < errors.Length ? errors[error] : "reserved code " + error));
            return string.Join("; ", states);
        case 23:
            return v + " cycles";
        case 26:
            int version = (v >> 4) & 15, revision = v & 15;
            return (version == 1   ? "SBS 1.0"
                    : version == 2 ? "SBS 1.1"
                    : version == 3 ? "SBS 1.1 with optional PEC"
                                   : "Unknown version " + version) +
                   "; revision field " + revision + (revision == 1 ? " (defined)" : " (reserved)") +
                   "; voltage ×" + (vs <= 3 ? vscale.ToString("0") : "unknown") + "; current/capacity ×" +
                   (ips <= 3 ? iscale.ToString("0") : "unknown");
        case 27:
            return DecodeDate(v);
        case 28:
            return v + " (pack-reported serial)";
        default:
            return "No verified decoder";
        }
    }
    internal static string DecodeBlock(string key, BatterySnapshot s)
    {
        if (key == "23")
            return s.Cells == null
                       ? "Cell data unavailable: " + s.CellError
                       : string.Join(" / ", s.Cells.Select((v, i) => "Cell " + (i + 1) + ": " +
                                                                     (v / 1000.0).ToString("F3") + " V")) +
                             "; spread " + (s.Cells.Max() - s.Cells.Min()) + " mV; other bytes unknown";
        // The firmware omits the block length: only established text prefixes are displayed.
        string text = BlockText(s.Blocks[key]);
        if (key == "22")
            return text.StartsWith("LION", StringComparison.OrdinalIgnoreCase)
                       ? "Lithium-ion (LION prefix; trailing bytes not decoded)"
                       : "Chemistry prefix: " + text + " (length unavailable)";
        return text + " (reported text prefix; length unavailable)";
    }
    internal static string RegisterHelp(byte cmd)
    {
        switch (cmd)
        {
        case 3:
            return "BatteryMode bit 15 selects capacity units: 1 = 10 mWh; 0 = mAh. Other bits are retained raw.";
        case 8:
            return "Pack temperature in 0.1 kelvin. Celsius = raw / 10 − 273.15. Not individual-cell temperature.";
        case 9:
            return "Battery-terminal voltage in mV. Divide by 1000 for volts.";
        case 10:
            return "Instantaneous signed 16-bit current in mA. Positive = charging; negative = discharging.";
        case 11:
            return "Signed average current in mA, averaged by the battery controller.";
        case 12:
            return "Controller's own estimated maximum state-of-charge error, in percent. Not independently verified.";
        case 13:
            return "Remaining charge as a percentage of the controller's learned full capacity.";
        case 14:
            return "Remaining charge as a percentage of design capacity; can differ from relative charge.";
        case 15:
            return "Remaining capacity. BatteryMode selects mAh or 10 mWh; dashboard converts to Ah or Wh.";
        case 16:
            return "Learned full-charge capacity. An estimate that calibration can revise; not a direct physical wear measurement.";
        case 17:
            return "Estimated minutes remaining at present discharge rate. 65535 means unavailable, not 65535 minutes.";
        case 18:
            return "Estimated minutes remaining at average discharge rate. 65535 means unavailable.";
        case 19:
            return "Estimated minutes until full at average charge rate. 65535 means unavailable.";
        case 20:
            return "Charging current requested by the pack, in mA. A zero request must be respected.";
        case 21:
            return "Charging voltage requested by the pack, in mV. Zero means no voltage is requested.";
        case 22:
            return "BatteryStatus bit field: standard charge/temperature alarms, state flags and a four-bit error code. See Status flags.";
        case 23:
            return "Cycle count reported directly by the pack. The BIOS ACPI count is hardcoded to zero on this laptop.";
        case 24:
            return "Pack's reported design capacity in mAh or 10 mWh according to BatteryMode.";
        case 25:
            return "Nominal design voltage in mV; not a target charging voltage.";
        case 26:
            return "SBS version, revision and voltage/current/capacity scale factors. Charge-current and charge-voltage requests are not scaled.";
        case 27:
            return "Manufacture date encoding: year = 1980 + (raw >> 9), month = (raw >> 5) & 15, day = raw & 31. Pack-reported, potentially cloned.";
        case 28:
            return "Pack-reported serial number. May be cloned by a replacement pack.";
        default:
            return "Raw SBS register; interpretation not established.";
        }
    }
    internal static string BlockHelp(string key)
    {
        switch (key)
        {
        case "20":
            return "Manufacturer-name ASCII prefix. May be cloned. Response has 32 bytes but valid length is unavailable; trailing data may be stale.";
        case "21":
            return "Device-name ASCII prefix. May be cloned. Trailing bytes may be stale because valid length is unavailable.";
        case "22":
            return "Device chemistry text prefix. Raw response tail may be stale; not proof of actual cell chemistry or model.";
        case "23":
            return "This pack's manufacturer data: header 42 01 01 01, four little-endian mV words at payload offsets 4–11. Remaining bytes are not decoded; response tail may be stale.";
        default:
            return "Raw manufacturer block; no verified interpretation.";
        }
    }
    internal static string StatusHelp(int bit)
    {
        string prefix = "SBS BatteryStatus bit " + bit + ". ";
        switch (bit)
        {
        case 15:
            return prefix +
                   "Pack asserts OVER_CHARGED_ALARM. A controller alarm alone does not prove physical cell overvoltage. Respect charge-stop requests.";
        case 14:
            return prefix +
                   "TERMINATE_CHARGE_ALARM: the pack requests charging stop. The app will not start calibration while set.";
        case 12:
            return prefix +
                   "OVER_TEMP_ALARM: pack reports excessive temperature. No specific cell threshold has been identified.";
        case 11:
            return prefix + "TERMINATE_DISCHARGE_ALARM: pack requests discharging stop.";
        case 9:
            return prefix + "Remaining capacity is below the battery's programmed alarm threshold.";
        case 8:
            return prefix + "Estimated remaining runtime is below the battery's programmed alarm threshold.";
        case 7:
            return prefix +
                   "INITIALIZED: controller considers its battery data initialized. Does not establish accurate calibration.";
        case 6:
            return prefix +
                   "DISCHARGING: battery state flag; can include self-discharge. Use signed current to determine net terminal flow.";
        case 5:
            return prefix +
                   "FULLY_CHARGED: controller's full-charge state flag. Distinct from numerical 100% charge.";
        case 4:
            return prefix +
                   "FULLY_DISCHARGED: controller's empty state flag. Do not use it to infer an unknown safe cell-voltage cutoff.";
        default:
            return prefix + "Reserved in the SBS status word. No interpretation is assigned.";
        }
    }
    internal static string BlockText(string hex)
    {
        byte[] bytes = Enumerable.Range(0, hex.Length / 2)
                           .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                           .TakeWhile(b => b >= 32 && b <= 126)
                           .ToArray();
        return Encoding.ASCII.GetString(bytes);
    }
}
