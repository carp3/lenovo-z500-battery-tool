using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

static class NativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(string p, uint access, uint share, IntPtr sa,
                                                     uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] input, int il,
                                                byte[] output, int ol, out int returned, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetSystemFirmwareTable(uint provider, uint id, byte[] data, uint size);
}
