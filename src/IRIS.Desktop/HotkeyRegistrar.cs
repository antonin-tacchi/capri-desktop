using System;
using System.Runtime.InteropServices;

namespace IRIS.Desktop;

internal interface IHotkeyRegistrar
{
    bool Register(IntPtr window, int functionKey);
    void Unregister(IntPtr window);
}

internal sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    public bool Register(IntPtr window, int functionKey) => RegisterHotKey(window, 1, 0x4003, (uint)(0x70 + functionKey - 1));
    public void Unregister(IntPtr window) => UnregisterHotKey(window, 1);
}
