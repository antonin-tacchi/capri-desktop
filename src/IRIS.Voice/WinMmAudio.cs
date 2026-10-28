using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace IRIS.Voice;

public sealed class AudioDriverShutdownException(string message) : IOException(message);

internal static class WinMm
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct Format
    {
        public short Tag, Channels;
        public int Rate, BytesPerSecond;
        public short Alignment, Bits, Extra;
        public static Format Pcm => new() { Tag = 1, Channels = 1, Rate = PcmAudio.SampleRate, BytesPerSecond = PcmAudio.SampleRate * 2, Alignment = 2, Bits = 16 };
    }
    [StructLayout(LayoutKind.Explicit, Size = 12)]
    internal struct Position
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Value;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Header
    {
        public IntPtr Data;
        public uint Length, Recorded;
        public UIntPtr User;
        public uint Flags, Loops;
        public IntPtr Next;
        public UIntPtr Reserved;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Capabilities
    {
        public ushort Manufacturer, Product;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public uint Formats;
        public ushort Channels, Reserved;
    }
    [DllImport("winmm.dll")] internal static extern uint waveInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] internal static extern uint waveInGetDevCaps(UIntPtr id, out Capabilities capabilities, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveInMessage(IntPtr device, uint message, out uint value, IntPtr unused);
    [DllImport("winmm.dll", EntryPoint = "waveInMessage", CharSet = CharSet.Unicode)] internal static extern uint waveInMessageText(IntPtr device, uint message, StringBuilder value, IntPtr size);
    [DllImport("winmm.dll")] internal static extern uint waveInGetID(IntPtr device, out uint id);
    [DllImport("winmm.dll")] internal static extern uint waveInOpen(out IntPtr device, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] internal static extern uint waveInPrepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveInUnprepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveInAddBuffer(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveInStart(IntPtr device);
    [DllImport("winmm.dll")] internal static extern uint waveInReset(IntPtr device);
    [DllImport("winmm.dll")] internal static extern uint waveInClose(IntPtr device);
    [DllImport("winmm.dll")] internal static extern uint waveOutOpen(out IntPtr device, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] internal static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] internal static extern uint waveOutReset(IntPtr device);
    [DllImport("winmm.dll")] internal static extern uint waveOutClose(IntPtr device);
    [DllImport("winmm.dll")] internal static extern uint waveOutGetPosition(IntPtr device, ref Position position, uint size);
    internal static uint HeaderSize => checked((uint)Marshal.SizeOf<Header>());
    internal static void Check(uint result, string operation) { if (result != 0) throw new IOException(operation + " (audio Windows " + result + ")."); }

    internal sealed class Buffer
    {
        public IntPtr Pointer { get; }
        public IntPtr Data { get; }
        public int Length { get; }
        public bool Prepared;
        public Header State => Marshal.PtrToStructure<Header>(Pointer);
        public Buffer(int length)
        {
            Length = length; Data = Marshal.AllocHGlobal(length); Pointer = Marshal.AllocHGlobal((int)HeaderSize);
            Marshal.Copy(new byte[length], 0, Data, length);
            Marshal.StructureToPtr(new Header { Data = Data, Length = (uint)length }, Pointer, false);
        }
        public void Free()
        {
            Marshal.Copy(new byte[Length], 0, Data, Length); Marshal.FreeHGlobal(Data); Marshal.FreeHGlobal(Pointer);
        }
    }
    // A failed driver cleanup must not free memory still referenced by native code.
    internal static readonly List<object> DriverFailures = new();
}

public sealed class WinMmAudioCapture : IAudioCapture
{
    private sealed record Device(uint Index, AudioInputDevice Info);
    private static List<Device> Devices()
    {
        var devices = new List<Device>();
        uint count = Math.Min(WinMm.waveInGetNumDevs(), 128);
        for (uint index = 0; index < count; index++)
        {
            if (WinMm.waveInGetDevCaps(new UIntPtr(index), out WinMm.Capabilities capabilities, (uint)Marshal.SizeOf<WinMm.Capabilities>()) != 0) continue;
            string id = "winmm:" + index + ":" + capabilities.Manufacturer + ":" + capabilities.Product + ":" + capabilities.Name;
            if (WinMm.waveInMessage(new IntPtr(index), 0x0812, out uint size, IntPtr.Zero) == 0 && size is > 0 and <= 8192)
            {
                var text = new StringBuilder((int)size / 2 + 1);
                if (WinMm.waveInMessageText(new IntPtr(index), 0x0811, text, new IntPtr(size)) == 0 && text.Length > 0) id = text.ToString();
            }
            devices.Add(new(index, new(id, capabilities.Name)));
        }
        return devices;
    }
    public IReadOnlyList<AudioInputDevice> GetDevices() => Devices().Select(d => d.Info).ToArray();
    public Task<PcmAudio> CaptureAsync(string deviceId, CaptureSettings settings, IProgress<AudioLevel>? progress, CancellationToken released, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(settings.Gain) || settings.Gain is < 0.5 or > 2 || !double.IsFinite(settings.SpeechThreshold) || settings.SpeechThreshold is < 0.001 or > 0.2)
            throw new ArgumentException("Gain ou seuil audio invalide.");
        return Task.Run(() => Capture(deviceId, settings, progress, released, cancellationToken), cancellationToken);
    }
    private static PcmAudio Capture(string deviceId, CaptureSettings settings, IProgress<AudioLevel>? progress, CancellationToken released, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (released.IsCancellationRequested) return new PcmAudio(ReadOnlySpan<byte>.Empty);
        Device selected = Devices().SingleOrDefault(d => d.Info.Id == deviceId) ?? throw new IOException("Le microphone sélectionné a disparu. Aucun autre périphérique n’a été ouvert.");
        var signal = new AutoResetEvent(false);
        IntPtr device = IntPtr.Zero;
        var buffers = new List<WinMm.Buffer>();
        using var recorded = new MemoryStream();
        byte[] scratch = new byte[3200];
        try
        {
            WinMm.Format format = WinMm.Format.Pcm;
            uint opened = WinMm.waveInOpen(out device, selected.Index, ref format, signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x00050000);
            if (opened != 0) { device = IntPtr.Zero; WinMm.Check(opened, "Ouverture du microphone refusée"); }
            WinMm.Check(WinMm.waveInGetID(device, out uint actual), "Identification du microphone impossible");
            if (actual != selected.Index || !Devices().Any(d => d.Index == actual && d.Info.Id == deviceId)) throw new IOException("Identité du microphone modifiée ; capture refusée.");
            for (int index = 0; index < 4; index++)
            {
                var buffer = new WinMm.Buffer(3200); buffers.Add(buffer);
                WinMm.Check(WinMm.waveInPrepareHeader(device, buffer.Pointer, WinMm.HeaderSize), "Préparation de capture impossible"); buffer.Prepared = true;
                WinMm.Check(WinMm.waveInAddBuffer(device, buffer.Pointer, WinMm.HeaderSize), "Ajout du tampon audio impossible");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (released.IsCancellationRequested) return new PcmAudio(ReadOnlySpan<byte>.Empty);
            WinMm.Check(WinMm.waveInStart(device), "Démarrage de capture impossible");
            var timer = Stopwatch.StartNew();
            WaitHandle[] waits = { signal, released.WaitHandle, cancellationToken.WaitHandle };
            int next = 0;
            var consumed = new HashSet<IntPtr>();
            void Append(WinMm.Buffer current)
            {
                WinMm.Header header = current.State;
                int length = Math.Min((int)header.Recorded & ~1, current.Length);
                length = Math.Min(length, PcmAudio.MaximumCaptureBytes - (int)recorded.Length);
                Marshal.Copy(current.Data, scratch, 0, length);
                AudioProcessing.ApplyGain(scratch.AsSpan(0, length), settings.Gain);
                AudioLevel level = AudioProcessing.Measure(scratch.AsSpan(0, length), timer.Elapsed); progress?.Report(level);
                recorded.Write(scratch, 0, length);
            }
            while (timer.Elapsed < TimeSpan.FromSeconds(30) && recorded.Length < PcmAudio.MaximumCaptureBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool stop = released.IsCancellationRequested;
                WinMm.Buffer buffer = buffers[next];
                while ((buffer.State.Flags & 1) != 0)
                {
                    Append(buffer);
                    if (buffer.State.Recorded == 0 || stop || recorded.Length >= PcmAudio.MaximumCaptureBytes) { consumed.Add(buffer.Pointer); break; }
                    WinMm.Check(WinMm.waveInAddBuffer(device, buffer.Pointer, WinMm.HeaderSize), "Réutilisation du tampon impossible");
                    next = (next + 1) % buffers.Count; buffer = buffers[next];
                }
                // Push-to-talk ends on release, not on a hesitation or a quiet voice.
                if (stop) break;
                WaitHandle.WaitAny(waits, 100);
            }
            cancellationToken.ThrowIfCancellationRequested();
            WinMm.Check(WinMm.waveInReset(device), "Fin de capture non confirmée");
            for (int index = 0; index < buffers.Count; index++)
            {
                WinMm.Buffer remaining = buffers[(next + index) % buffers.Count];
                if (!consumed.Contains(remaining.Pointer)) Append(remaining);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new PcmAudio(recorded.GetBuffer().AsSpan(0, (int)recorded.Length)); // Preprocessing runs after microphone closure.
        }
        finally
        {
            bool clean = true;
            if (device != IntPtr.Zero)
            {
                clean = WinMm.waveInReset(device) == 0;
                foreach (WinMm.Buffer buffer in buffers)
                    if (buffer.Prepared && WinMm.waveInUnprepareHeader(device, buffer.Pointer, WinMm.HeaderSize) != 0) clean = false;
                if (WinMm.waveInClose(device) != 0) clean = false;
            }
            if (recorded.TryGetBuffer(out ArraySegment<byte> segment)) CryptographicOperations.ZeroMemory(segment.AsSpan());
            CryptographicOperations.ZeroMemory(scratch);
            if (clean) { foreach (WinMm.Buffer buffer in buffers) buffer.Free(); signal.Dispose(); }
            else
            {
                lock (WinMm.DriverFailures) WinMm.DriverFailures.Add(new object[] { signal, buffers, device });
                throw new AudioDriverShutdownException("Le pilote audio ne confirme pas la fermeture du microphone. Fermez IRIS avant de recommencer.");
            }
        }
    }
}

public sealed class WinMmAudioPlayback : IAudioPlayback
{
    public Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? progress, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (audio.Data.Length == 0) return;
        var signal = new AutoResetEvent(false);
        var buffer = new WinMm.Buffer(audio.Data.Length);
        Marshal.Copy(audio.Data.ToArray(), 0, buffer.Data, buffer.Length);
        IntPtr device = IntPtr.Zero;
        try
        {
            WinMm.Format format = WinMm.Format.Pcm;
            format.Rate = audio.SampleRateHz; format.BytesPerSecond = audio.SampleRateHz * 2;
            uint opened = WinMm.waveOutOpen(out device, uint.MaxValue, ref format, signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x00050000);
            if (opened != 0) { device = IntPtr.Zero; WinMm.Check(opened, "Sortie audio indisponible"); }
            WinMm.Check(WinMm.waveOutPrepareHeader(device, buffer.Pointer, WinMm.HeaderSize), "Préparation de lecture impossible"); buffer.Prepared = true;
            cancellationToken.ThrowIfCancellationRequested();
            WinMm.Check(WinMm.waveOutWrite(device, buffer.Pointer, WinMm.HeaderSize), "Lecture audio impossible");
            var timer = Stopwatch.StartNew();
            WaitHandle[] waits = { signal, cancellationToken.WaitHandle };
            while ((buffer.State.Flags & 1) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timer.Elapsed > audio.Duration.Add(TimeSpan.FromSeconds(5))) throw new IOException("Délai de lecture dépassé.");
                var position = new WinMm.Position { Type = 4 };
                bool precise = WinMm.waveOutGetPosition(device, ref position, 12) == 0 && position.Type is 1 or 2 or 4;
                double seconds = precise ? position.Type == 4 ? position.Value / (audio.SampleRateHz * 2.0) : position.Type == 2 ? position.Value / (double)audio.SampleRateHz : position.Value / 1000.0 : timer.Elapsed.TotalSeconds;
                TimeSpan played = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, audio.Duration.TotalSeconds));
                int offset = Math.Min((int)(played.TotalSeconds * audio.SampleRateHz) * 2, audio.Data.Length) & ~1;
                int count = Math.Min(3200, audio.Data.Length - offset);
                progress?.Report(AudioProcessing.Measure(audio.Data.Span.Slice(offset, count), played) with { DevicePosition = precise });
                WaitHandle.WaitAny(waits, 25);
            }
        }
        finally
        {
            bool clean = true;
            if (device != IntPtr.Zero)
            {
                clean = WinMm.waveOutReset(device) == 0;
                if (buffer.Prepared && WinMm.waveOutUnprepareHeader(device, buffer.Pointer, WinMm.HeaderSize) != 0) clean = false;
                if (WinMm.waveOutClose(device) != 0) clean = false;
            }
            if (clean) { buffer.Free(); signal.Dispose(); }
            else { lock (WinMm.DriverFailures) WinMm.DriverFailures.Add(new object[] { signal, buffer, device }); throw new AudioDriverShutdownException("Le pilote audio ne confirme pas l’arrêt de lecture. Fermez IRIS."); }
        }
    }, cancellationToken);
}
