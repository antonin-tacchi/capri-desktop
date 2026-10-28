using System;
using System.Windows.Forms;
using IRIS.Actions;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Infrastructure;
using IRIS.Security.Permissions;
using IRIS.Security.Policies;
using IRIS.Infrastructure.Inference;
using IRIS.Memory;
using System.Collections.Generic;
using IRIS.Voice;
using IRIS.Infrastructure.Voice;
using IRIS.Desktop.Animation;
using System.IO;

namespace IRIS.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 1 && args[0] == "--self-check") { Environment.ExitCode = InstalledSelfCheck.Run(); return; }
        if (args.Length == 1 && args[0] == "--speech-check") { Environment.ExitCode = InstalledSelfCheck.RunSpeechCheck(); return; }
        SingleInstanceLease? lease;
        try { lease = SingleInstanceLease.TryAcquire(LocalSettingsStore.DefaultDirectory); }
        catch (Exception)
        {
            MessageBox.Show("Impossible d’accéder au dossier de données IRIS.", "IRIS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        using var instance = lease;
        if (instance == null)
        {
            MessageBox.Show("IRIS est déjà ouverte pour ce dossier de données.", "IRIS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var store = new LocalSettingsStore(LocalSettingsStore.DefaultDirectory);
        IrisSettings settings;
        try { settings = store.Load(); }
        catch (Exception)
        {
            MessageBox.Show("Impossible de lire les paramètres IRIS. Le fichier existant est conservé.\nVérifiez settings.json dans %LOCALAPPDATA%\\IRIS.", "IRIS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        using var emergency = new EmergencyStopService();
        string engineRoot = EngineAssets.Resolve(AppContext.BaseDirectory, LocalSettingsStore.DefaultDirectory);
        var inference = new LocalInferenceService(engineRoot, emergency);
        var memory = new MemoryService(LocalSettingsStore.DefaultDirectory);
        var windowsVoice = new WindowsTextToSpeechProvider();
        var synthesis = new CompositeSpeechProvider(new PiperSynthesisProvider(engineRoot, windowsVoice), windowsVoice);
        var voice = new VoiceServices(new WinMmAudioCapture(), new ISpeechRecognitionProvider[] { new WhisperRecognitionProvider(engineRoot), new WindowsSpeechRecognitionProvider() },
            synthesis, new WinMmAudioPlayback());
        AnimationCatalog? animations = null;
        string? animationError = null;
        try { animations = new AnimationCatalog(Path.Combine(AppContext.BaseDirectory, "assets")); }
        catch (Exception error) { animationError = "Animations indisponibles : " + error.Message; }
        using var window = new MainWindow(store, settings, emergency, inference: inference, memory: memory, voice: voice, animations: animations, animationError: animationError, engineRoot: engineRoot);
        var actions = new List<IActionHandler> { new LocalClockAction(false), new LocalClockAction(true) };
        foreach (string operation in new[] { "add", "update", "delete", "clear", "policy", "import" }) actions.Add(new MemoryActionHandler(memory, operation));
        var policy = new ActionPolicyEngine(actions,
            new PermissionService(), new WindowsConfirmationService(window, window.Stop), new JsonActionAudit(LocalSettingsStore.DefaultDirectory), emergency);
        window.SetPolicy(policy);
        try { Application.Run(window); }
        finally
        {
            emergency.Stop();
            inference.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
