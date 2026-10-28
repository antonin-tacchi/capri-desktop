using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IRIS.AI.Inference;
using IRIS.Core.Services;

namespace IRIS.Infrastructure.Inference;

[SupportedOSPlatform("windows")]
public sealed class LocalInferenceService : ILocalInferenceService, IAsyncDisposable
{
    private readonly string root;
    private readonly EmergencyStopService emergency;
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly CancellationTokenRegistration emergencyRegistration;
    private Process? child;
    private WindowsProcessJob? job;
    private VerifiedModelFiles? files;
    private LocalCompletionClient? client;
    private bool disposed;
    private bool ready;
    private int port;
    public bool IsReady => ready && client != null && child is { HasExited: false };
    public int? ProcessId => child is { HasExited: false } ? child.Id : null;
    public bool CpuOnly { get; private set; }

    public LocalInferenceService(string root, EmergencyStopService emergency)
    {
        this.root = root;
        this.emergency = emergency;
        emergencyRegistration = emergency.Token.Register(Kill);
    }

    private void Kill()
    {
        try { Process? process = child; if (process is { HasExited: false }) process.Kill(true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public async Task StartAsync(bool cpuOnly, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, emergency.Token);
        await serial.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            linked.Token.ThrowIfCancellationRequested();
            if (IsReady && CpuOnly == cpuOnly) return;
            await StopCoreAsync().ConfigureAwait(false);
            files = await VerifiedModelFiles.LoadAsync(root, linked.Token).ConfigureAwait(false);
            job = new WindowsProcessJob();
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string apiKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var start = new ProcessStartInfo(files.ServerPath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = files.RuntimeDirectory
            };
            start.Environment.Clear();
            foreach (string name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "USERPROFILE" })
                if (Environment.GetEnvironmentVariable(name) is string value) start.Environment[name] = value;
            start.Environment["PATH"] = files.RuntimeDirectory + Path.PathSeparator + Environment.SystemDirectory;
            start.Environment["LLAMA_API_KEY"] = apiKey;
            foreach (string argument in new[] { "-m", files.ModelPath, "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-c", "4096", "-np", "1", "-a", "iris-local", "--offline", "--no-webui", "--log-disable", "--jinja",
                "-t", "4", "-tb", "4", "--poll", "0", "--poll-batch", "0", "-ngl", cpuOnly ? "0" : "99" }) start.ArgumentList.Add(argument);
            if (cpuOnly) { start.ArgumentList.Add("--device"); start.ArgumentList.Add("none"); }
            linked.Token.ThrowIfCancellationRequested();
            child = WindowsJobLauncher.Start(start, job);
            child.PriorityClass = ProcessPriorityClass.BelowNormal;
            client = new LocalCompletionClient(new Uri($"http://127.0.0.1:{port}/"), apiKey);
            CpuOnly = cpuOnly;
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(120));
            while (true)
            {
                startup.Token.ThrowIfCancellationRequested();
                if (child.HasExited) throw new IOException("Le moteur local s’est arrêté au chargement (code " + child.ExitCode + "). Essayez le mode CPU.");
                if (WindowsListenerInspector.Verify(child.Id, port))
                {
                    try { if (await client.IsHealthyAsync(startup.Token).ConfigureAwait(false)) break; }
                    catch (HttpRequestException) { }
                    catch (OperationCanceledException) when (!startup.IsCancellationRequested) { }
                }
                await Task.Delay(200, startup.Token).ConfigureAwait(false);
            }
            ready = true;
        }
        catch { await StopCoreAsync().ConfigureAwait(false); throw; }
        finally { serial.Release(); }
    }

    public async Task<string> GenerateAsync(IReadOnlyList<ChatEntry> messages, GenerationSettings settings, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, emergency.Token);
        await serial.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsReady || client == null || child == null) throw new InvalidOperationException("Chargez d’abord le modèle local.");
            if (!WindowsListenerInspector.Verify(child.Id, port)) throw new IOException("Écoute locale du moteur indisponible.");
            string answer = await client.GenerateAsync(messages, settings, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            return answer;
        }
        catch { await StopCoreAsync().ConfigureAwait(false); throw; }
        finally { serial.Release(); }
    }

    private async Task StopCoreAsync()
    {
        ready = false;
        Kill();
        job?.Dispose(); job = null;
        if (child != null)
        {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            child.Dispose(); child = null;
        }
        client?.Dispose(); client = null;
        files?.Dispose(); files = null;
    }

    public async Task StopAsync()
    {
        await serial.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { serial.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            emergencyRegistration.Dispose();
            await StopCoreAsync().ConfigureAwait(false);
            disposed = true;
        }
        finally { serial.Release(); }
    }
}
