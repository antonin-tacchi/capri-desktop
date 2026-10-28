using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace IRIS.Infrastructure.Inference;

[SupportedOSPlatform("windows")]
internal static class WindowsListenerInspector
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

    public static bool Verify(int processId, int port)
    {
        bool ready = false;
        foreach (int family in new[] { 2, 23 })
        {
            int size = 0;
            uint status = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 3, 0);
            if (status != 122) throw new Win32Exception((int)status);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                status = GetExtendedTcpTable(buffer, ref size, false, family, 3, 0);
                if (status != 0) throw new Win32Exception((int)status);
                int count = Marshal.ReadInt32(buffer);
                int rowSize = family == 2 ? 24 : 56;
                if (count < 0 || count > (size - 4) / rowSize) throw new InvalidOperationException("Table TCP invalide.");
                for (int index = 0; index < count; index++)
                {
                    IntPtr row = IntPtr.Add(buffer, 4 + index * rowSize);
                    int owner = Marshal.ReadInt32(row, family == 2 ? 20 : 52);
                    if (owner != processId) continue;
                    byte[] address = new byte[family == 2 ? 4 : 16];
                    Marshal.Copy(IntPtr.Add(row, family == 2 ? 4 : 0), address, 0, address.Length);
                    int encodedPort = Marshal.ReadInt32(row, family == 2 ? 8 : 20);
                    int localPort = ((encodedPort & 255) << 8) | ((encodedPort >> 8) & 255);
                    IPAddress ip = new(address);
                    if (!ip.Equals(IPAddress.Loopback) || localPort != port)
                        throw new InvalidOperationException("Le moteur a ouvert une écoute inattendue. Arrêt imposé.");
                    ready = true;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return ready;
    }
}
