using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Infrastructure.Inference;
using IRIS.Infrastructure.Native;

namespace IRIS.Infrastructure.Voice;

[SupportedOSPlatform("windows")]
public sealed class PiperSynthesisProvider(string root, ITimedTextToSpeechProvider? timingReference = null) : ITimedTextToSpeechProvider
{
    public const string VoiceId = "piper-fr-siwis-medium";
    public const string ArchiveSha = "f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea";
    public const string ModelSha = "641d1ab097da2b81128c076810edb052b385decc8be3381814802a64a73baf99";
    public const string ConfigSha = "39479916c2db192b5ac9764daddd0c744d83e023ad890c6976c0633ae4df8959";
    private static readonly TrustedModelPackage Package = new("piper-2023.11.14-2-windows-amd64.zip", ArchiveSha, "piper-2023.11.14-2", "fr_FR-siwis-medium.onnx", ModelSha, 63201294);
    private string Executable => Path.Combine(root, "runtimes", Package.RuntimeName, "piper", "piper.exe");
    public bool IsAvailable => File.Exists(Executable) && File.Exists(Path.Combine(root, "packages", Package.ArchiveName)) && File.Exists(Path.Combine(root, "models", Package.ModelName)) && File.Exists(Path.Combine(root, "models", Package.ModelName + ".json"));
    public IReadOnlyList<LocalVoice> GetVoices() => IsAvailable ? new[] { new LocalVoice(VoiceId, "Siwis · français · voix neuronale locale", "fr-FR", true) } : Array.Empty<LocalVoice>();
    public async Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken token) => (await GenerateAsync(text, settings, token).ConfigureAwait(false));
    public async Task<TimedSpeech> SynthesizeTimedAsync(string text, SpeechSynthesisSettings settings, CancellationToken token)
    {
        PcmAudio audio = await GenerateAsync(text, settings, token).ConfigureAwait(false);
        try
        {
            IReadOnlyList<SpeechWordBoundary> marks = SpeechTiming.Estimate(text, audio.Duration);
            // Siwis supplies no word timestamps. Use a French phonetic reference's
            // relative word positions, scaled to this clip. These remain estimates.
            if (timingReference != null)
            {
                try
                {
                LocalVoice? reference = timingReference.GetVoices().FirstOrDefault();
                if (reference != null)
                {
                    using TimedSpeech timing = await timingReference.SynthesizeTimedAsync(text, new(reference.Id, 80, settings.Rate), token).ConfigureAwait(false);
                    if (timing.Words.Count > 0 && timing.Audio.Duration.TotalSeconds > 0)
                        marks = timing.Words.Select(m => m with { Position = TimeSpan.FromSeconds(Math.Clamp(m.Position.TotalSeconds / timing.Audio.Duration.TotalSeconds, 0, 1) * audio.Duration.TotalSeconds) }).ToArray();
                }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { /* A missing SAPI reference must not disable neural speech. */ }
            }
            return new(audio, marks, true);
        }
        catch { audio.Dispose(); throw; }
    }
    private async Task<PcmAudio> GenerateAsync(string text, SpeechSynthesisSettings settings, CancellationToken token)
    {
        if (settings.VoiceId != VoiceId || string.IsNullOrWhiteSpace(text) || text.Length > 700 || settings.Rate is < -5 or > 5 || settings.Volume is < 0 or > 100) throw new ArgumentException("Paramètres de synthèse Piper invalides.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(90));
        using VerifiedFileSet files = await VerifiedFileSet.LoadAsync(root, Package, deadline.Token).ConfigureAwait(false);
        string model = Path.Combine(root, "models", Package.ModelName);
        using var config = new FileStream(model + ".json", FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Convert.ToHexStringLower(await SHA256.HashDataAsync(config, deadline.Token).ConfigureAwait(false)) != ConfigSha) throw new InvalidDataException("Configuration Siwis non conforme.");
        using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        using var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        using var error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        using var job = new WindowsProcessJob();
        var start = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(Executable)! };
        start.Environment.Clear();
        foreach (string key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "USERPROFILE" }) if (Environment.GetEnvironmentVariable(key) is string value) start.Environment[key] = value;
        start.Environment["PATH"] = start.WorkingDirectory + Path.PathSeparator + Environment.SystemDirectory;
        double lengthScale = Math.Pow(1.08, -settings.Rate);
        foreach (string argument in new[] { "--model", model, "--config", model + ".json", "--json-input", "--output-raw", "--length-scale", lengthScale.ToString(System.Globalization.CultureInfo.InvariantCulture), "--sentence-silence", "0.16" }) start.ArgumentList.Add(argument);
        deadline.Token.ThrowIfCancellationRequested();
        using Process process = WindowsJobLauncher.Start(start, job, input.ClientSafePipeHandle.DangerousGetHandle(), output.ClientSafePipeHandle.DangerousGetHandle(), error.ClientSafePipeHandle.DangerousGetHandle());
        input.DisposeLocalCopyOfClientHandle(); output.DisposeLocalCopyOfClientHandle(); error.DisposeLocalCopyOfClientHandle();
        using var cancellation = deadline.Token.Register(job.Dispose);
        byte[] request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { text }) + "\n");
        Task write = WriteAsync(input, request, deadline.Token);
        Task<byte[]> read = ReadAsync(output, 22050 * 2 * 120, deadline.Token);
        Task<byte[]> discard = ReadAsync(error, 1048576, deadline.Token);
        try
        {
            WindowsListenerInspector.Verify(process.Id, -1);
            await Task.WhenAll(write, read, discard, process.WaitForExitAsync(deadline.Token)).WaitAsync(deadline.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("Le moteur Siwis a échoué (code " + process.ExitCode + ").");
            byte[] raw = await read.ConfigureAwait(false);
            try
            {
                if (raw.Length == 0 || raw.Length % 2 != 0) throw new InvalidDataException("Audio Siwis vide ou invalide.");
                for (int i = 0; i < raw.Length; i += 2)
                {
                    short sample = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(i));
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(i), (short)(sample * settings.Volume / 100));
                }
                return new PcmAudio(raw, 22050);
            }
            finally { CryptographicOperations.ZeroMemory(raw); }
        }
        finally
        {
            job.Dispose(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            input.Dispose(); output.Dispose(); error.Dispose();
            try { await Task.WhenAll(write, read, discard).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch (Exception) { }
            CryptographicOperations.ZeroMemory(request);
            if (read.IsCompletedSuccessfully) CryptographicOperations.ZeroMemory(read.Result);
            if (discard.IsCompletedSuccessfully) CryptographicOperations.ZeroMemory(discard.Result);
        }
    }
    private static async Task WriteAsync(Stream input, byte[] data, CancellationToken token)
    { try { await input.WriteAsync(data, token).ConfigureAwait(false); } finally { input.Dispose(); } }
    private static async Task<byte[]> ReadAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var memory = new MemoryStream(); byte[] buffer = new byte[8192];
        try
        {
            int size;
            while ((size = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            { if (memory.Length + size > maximum) throw new InvalidDataException("Sortie Piper trop grande."); memory.Write(buffer, 0, size); }
            return memory.ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(buffer); if (memory.TryGetBuffer(out var bytes)) CryptographicOperations.ZeroMemory(bytes.AsSpan()); }
    }
}
