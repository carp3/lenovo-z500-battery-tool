using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

sealed class LenovoAcpiBridge : IDisposable
{
    internal const string FirmwareHash = "3aa6787391cf1e44a0632e5c63f2ead524fcbcecf88cd49aef5de498d3db735f";
    internal const string DriverHash = "6bc61af7806a6482754af7757ae7dd013d345bcc92e18901f6474fefc3e07c8d";
    SafeFileHandle handle;
    internal static string Hash(byte[] b)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b))
            .Replace("-", "")
            .ToLowerInvariant();
    }
    internal LenovoAcpiBridge()
    {
        uint len = NativeMethods.GetSystemFirmwareTable(0x41435049, 0x54445344, null, 0);
        if (len == 0 || len > 1024 * 1024)
            throw new Exception("Cannot read DSDT: " + Marshal.GetLastWin32Error());
        byte[] table = new byte[len];
        if (NativeMethods.GetSystemFirmwareTable(0x41435049, 0x54445344, table, len) != len ||
            Hash(table) != FirmwareHash)
            throw new Exception("DSDT differs from inspected Z500 firmware. Direct access stopped.");
        if (Hash(File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                                @"System32\drivers\AcpiVpc.sys"))) != DriverHash)
            throw new Exception(
                "Lenovo driver differs from inspected version 15.11.29.70. Direct access stopped.");
        handle = NativeMethods.CreateFile(@"\\.\EnergyDrv", 0xc0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    byte[] Io(uint code, byte[] data, int length, bool allowEmpty = false)
    {
        byte[] output = new byte[length];
        int returned;
        if (!NativeMethods.DeviceIoControl(handle, code, data, data.Length, output, output.Length,
                                           out returned, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (returned != length && !(allowEmpty && returned == 0))
            throw new Exception("Unexpected Lenovo response length: " + returned);
        return output;
    }
    internal uint Mode()
    {
        return BitConverter.ToUInt32(Io(0x831020f8, new byte[] { 255, 0, 0, 0 }, 4), 0);
    }
    internal void Calibration(bool start)
    {
        if (!BuildOptions.EnableExperimentalCalibration)
            throw new InvalidOperationException("Calibration is disabled in this build.");
        Io(0x831020f8, new byte[] { (byte)(start ? 1 : 0), 0, 0, 0 }, 4, true);
    }
    internal byte[] Read(byte command, bool block)
    {
        // Only the inspected SBS read commands are exposed; no arbitrary write operation.
        byte[] allowed = { 3,  8,  9,  10, 11, 12, 13, 14, 15, 16, 17,
                           18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 };
        if (block ? command != 0x20 && command != 0x21 && command != 0x22 && command != 0x23
                  : !allowed.Contains(command))
            throw new Exception("Command not allowlisted");
        var request = new byte[37];
        request[0] = (byte)(block ? 11 : 9);
        request[2] = 0x16;
        request[3] = command;
        var reply = Io(0x83102114, request, 37);
        if (reply[0] != request[0] || reply[2] != 0x16 || reply[3] != command || reply[1] != 0x80)
            throw new Exception("LenovoAcpiBridge busy/error or mismatched SBS reply for 0x" +
                                command.ToString("X2") + ": " + BitConverter.ToString(reply));
        Thread.Sleep(80);
        return reply.Skip(4).Take(block ? 32 : 2).ToArray();
    }
    internal int Word(byte command)
    {
        return BitConverter.ToUInt16(Read(command, false), 0);
    }
    public void Dispose()
    {
        if (handle != null)
            handle.Dispose();
    }
}
