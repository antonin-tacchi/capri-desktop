using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using IRIS.Core.Contracts;
using IRIS.Infrastructure.Inference;
using IRIS.Infrastructure.Native;

namespace IRIS.Infrastructure.Voice;

[SupportedOSPlatform("windows")]
public sealed class WhisperRecognitionProvider(string root) : ISpeechRecognitionProvider
{
    private static readonly TrustedModelPackage Package = new("whisper-b5454-x64.zip", "6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb", "whisper-b5454", "ggml-small.bin", "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b", 487601967);
    private string Executable => Path.Combine(root, "runtimes", "whisper-b5454", "Release", "whisper-cli.exe");
    public string Id => "whisper-small-fr";
    public string Name => "Whisper Small multilingue · français · CPU";
    public bool IsAvailable => File.Exists(Executable) && File.Exists(Path.Combine(root, "models", Package.ModelName)) && File.Exists(Path.Combine(root, "packages", Package.ArchiveName));

    public async Task<SpeechRecognitionResult> RecognizeAsync(PcmAudio audio, CancellationToken cancellationToken)
    {
        if (audio.SampleRateHz != 16000 || audio.Data.Length > PcmAudio.MaximumCaptureBytes) throw new ArgumentException("Reconnaissance limitée à 30 secondes à 16 kHz.");
        if (audio.Data.Length == 0) return new("", null, Id);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        using VerifiedFileSet files = await VerifiedFileSet.LoadAsync(root, Package, deadline.Token).ConfigureAwait(false);
        using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        using var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        using var error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        using var job = new WindowsProcessJob();
        var start = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(Executable)! };
        start.Environment.Clear();
        foreach (string name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "USERPROFILE" })
            if (Environment.GetEnvironmentVariable(name) is string value) start.Environment[name] = value;
        start.Environment["PATH"] = start.WorkingDirectory + Path.PathSeparator + Environment.SystemDirectory;
        // With stdin named "-", this CLI otherwise disables its stdout segment callback.
        // An unused output base restores the callback; no file-output format is enabled.
        foreach (string argument in new[] { "--model", Path.Combine(root, "models", Package.ModelName), "--file", "-", "--output-file", "iris-stdout-only", "--language", "fr", "--threads", "4", "--no-gpu", "--no-prints", "--no-timestamps" }) start.ArgumentList.Add(argument);
        deadline.Token.ThrowIfCancellationRequested();
        using Process process = WindowsJobLauncher.Start(start, job, input.ClientSafePipeHandle.DangerousGetHandle(), output.ClientSafePipeHandle.DangerousGetHandle(), error.ClientSafePipeHandle.DangerousGetHandle());
        input.DisposeLocalCopyOfClientHandle(); output.DisposeLocalCopyOfClientHandle(); error.DisposeLocalCopyOfClientHandle();
        using var registration = deadline.Token.Register(job.Dispose);
        byte[] wave = audio.ToWave();
        Task write = WriteAsync(input, wave, deadline.Token);
        Task<string> read = ReadAsync(output, 32768, deadline.Token);
        Task<string> discard = ReadAsync(error, 1048576, deadline.Token);
        try
        {
            WindowsListenerInspector.Verify(process.Id, -1); // Any listening socket is forbidden for this CLI.
            await Task.WhenAll(write, read, discard, process.WaitForExitAsync(deadline.Token)).WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new IOException("Whisper a échoué (code " + process.ExitCode + ").");
            string text = (await read.ConfigureAwait(false)).Trim();
            if (text.Length > 4096) throw new InvalidDataException("Transcription trop longue.");
            return new(text, null, Id);
        }
        finally
        {
            job.Dispose();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            input.Dispose(); output.Dispose(); error.Dispose();
            try { await Task.WhenAll(write, read, discard).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException) { }
            CryptographicOperations.ZeroMemory(wave);
        }
    }
    private static async Task WriteAsync(AnonymousPipeServerStream stream, byte[] data, CancellationToken token)
    {
        try { await stream.WriteAsync(data, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); }
        finally { stream.Dispose(); }
    }
    private static async Task<string> ReadAsync(Stream stream, int limit, CancellationToken token)
    {
        using var data = new MemoryStream();
        byte[] buffer = new byte[4096];
        try
        {
            int count;
            while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (data.Length + count > limit) throw new InvalidDataException("Sortie vocale native trop volumineuse.");
                data.Write(buffer, 0, count);
            }
            return new UTF8Encoding(false, true).GetString(data.ToArray());
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }
}
