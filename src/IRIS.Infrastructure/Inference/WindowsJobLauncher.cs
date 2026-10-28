using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace IRIS.Infrastructure.Inference;

[SupportedOSPlatform("windows")]
internal static class WindowsJobLauncher
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, Width, Height, CharactersX, CharactersY, Fill, Flags;
        public short ShowWindow, ReservedCount;
        public IntPtr ReservedData, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo Startup; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Size; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnedSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes attributes, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processSecurity, IntPtr threadSecurity,
        bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(SafeFileHandle process, uint code);

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    public static Process Start(ProcessStartInfo start, WindowsProcessJob job, IntPtr? input = null, IntPtr? output = null, IntPtr? error = null)
    {
        var security = new SecurityAttributes { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        using SafeFileHandle sink = CreateFile("NUL", 0xC0000000, 3, ref security, 3, 0x80, IntPtr.Zero);
        if (sink.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
        if (size == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr attributes = Marshal.AllocHGlobal(size);
        IntPtr jobValue = Marshal.AllocHGlobal(IntPtr.Size);
        IntPtr[] inherited = new[] { input ?? sink.DangerousGetHandle(), output ?? sink.DangerousGetHandle(), error ?? sink.DangerousGetHandle() }.Distinct().ToArray();
        IntPtr sinkValue = Marshal.AllocHGlobal(IntPtr.Size * inherited.Length);
        IntPtr environment = IntPtr.Zero;
        int environmentBytes = 0;
        bool initialized = false;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            Marshal.WriteIntPtr(jobValue, job.Handle);
            for (int index = 0; index < inherited.Length; index++) Marshal.WriteIntPtr(sinkValue, index * IntPtr.Size, inherited[index]);
            // Assign the job atomically; inherit only the explicit standard handles.
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x2000D), jobValue, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero)
                || !UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), sinkValue, new IntPtr(IntPtr.Size * inherited.Length), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            string block = string.Join('\0', start.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
            environment = Marshal.StringToHGlobalUni(block);
            environmentBytes = checked((block.Length + 1) * 2);
            var startup = new StartupInfoEx
            {
                Attributes = attributes,
                Startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100, Input = input ?? sink.DangerousGetHandle(), Output = output ?? sink.DangerousGetHandle(), Error = error ?? sink.DangerousGetHandle() }
            };
            var commandLine = new StringBuilder(string.Join(' ', new[] { start.FileName }.Concat(start.ArgumentList).Select(Quote)));
            if (!CreateProcess(start.FileName, commandLine, IntPtr.Zero, IntPtr.Zero, true, 0x00080000 | 0x08000000 | 0x00000400,
                environment, start.WorkingDirectory, ref startup, out ProcessInfo information)) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var processHandle = new SafeFileHandle(information.Process, true);
            using var threadHandle = new SafeFileHandle(information.Thread, true);
            try
            {
                Process process = Process.GetProcessById(checked((int)information.ProcessId));
                process.EnableRaisingEvents = true;
                return process;
            }
            catch { TerminateProcess(processHandle, 1); throw; }
        }
        finally
        {
            if (environment != IntPtr.Zero)
            {
                Marshal.Copy(new byte[environmentBytes], 0, environment, environmentBytes);
                Marshal.FreeHGlobal(environment);
            }
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(jobValue);
            Marshal.FreeHGlobal(sinkValue);
        }
    }
}
