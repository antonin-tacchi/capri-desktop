using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.AI.Conversation;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Infrastructure;
using IRIS.Infrastructure.Inference;
using IRIS.Infrastructure.Voice;
using IRIS.Voice;
using IRIS.Desktop.Speech;
using System.Drawing;
using System.Drawing.Imaging;

namespace IRIS.Desktop;

// Explicit diagnostic of the deployed EXE, not the developer's test host.
// Only synthetic audio; no microphone, speaker, personal memories or PC actions.
internal static class InstalledSelfCheck
{
    private static T Find<T>(Control form, string name) where T : Control => (T)form.Controls.Find(name, true).Single();
    private static async Task UntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) await Task.Delay(20, token);
    }
    public static int Run()
    {
        int code = 1;
        using var host = new Form { Opacity = 0, ShowInTaskbar = false };
        host.Shown += async (_, _) =>
        {
            var steps = new List<object>();
            string engineRoot = EngineAssets.Resolve(AppContext.BaseDirectory, LocalSettingsStore.DefaultDirectory);
            string? failure = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var emergency = new EmergencyStopService();
            await using var inference = new LocalInferenceService(engineRoot, emergency);
            try
            {
                if (engineRoot.Equals(Path.GetFullPath(LocalSettingsStore.DefaultDirectory), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Le paquet dépend encore du cache utilisateur : les ressources portables ne sont pas trouvées.");
                foreach (string missing in EngineAssets.Missing(engineRoot, false).Concat(EngineAssets.Missing(engineRoot, true)))
                    throw new FileNotFoundException("Fichier livré absent", missing);
                var windowsVoice = new WindowsTextToSpeechProvider();
                var synthesis = new CompositeSpeechProvider(new PiperSynthesisProvider(engineRoot, windowsVoice), windowsVoice);
                using var bubble = new SpeechBubbleWindow(Path.Combine(AppContext.BaseDirectory, "assets"), host, () => host.Bounds);
                var muted = new MutedPlayback(() =>
                {
                    if (!bubble.Visible) throw new InvalidOperationException("Audio sans bulle.");
                });
                using var reader = new ReplySpeechController(synthesis, muted, bubble, () => new(), _ => { }, emergency.Stop, emergency.Token);
                using var chat = new ConversationWindow(inference, () => new PersonalityProfile(), emergency, emergency.Stop, engineRoot: engineRoot, speech: reader) { Opacity = 0, ShowInTaskbar = false };
                chat.Show(host);
                TextBox composer = Find<TextBox>(chat, "Input");
                if (!composer.Enabled || composer.ReadOnly) throw new InvalidOperationException("Saisie inaccessible.");
                Find<CheckBox>(chat, "CpuOnly").Checked = true;
                Find<Button>(chat, "Load").PerformClick();
                await UntilAsync(() => Find<Button>(chat, "Send").Enabled || Find<Button>(chat, "Load").Enabled, timeout.Token);
                if (!inference.IsReady) throw new InvalidOperationException(Find<Label>(chat, "Status").Text);
                steps.Add(new { check = "deployed_cpu_model_load", passed = true, root = engineRoot });
                using Process child = Process.GetProcessById(inference.ProcessId!.Value);
                composer.Text = "Bonjour. Présente-toi en une phrase en français.";
                Find<Button>(chat, "Send").PerformClick();
                await UntilAsync(() => Find<Button>(chat, "Send").Enabled || Find<Button>(chat, "Load").Enabled, timeout.Token);
                string response = Find<RichTextBox>(chat, "Transcript").Text;
                if (!response.Contains("IRIS :", StringComparison.Ordinal)) throw new InvalidOperationException("Aucune réponse : " + Find<Label>(chat, "Status").Text);
                steps.Add(new { check = "deployed_chat_response", passed = true, fixtureResponse = response });
                await UntilAsync(() => !reader.IsBusy, timeout.Token);
                if (muted.Calls == 0 || bubble.FontName != "Share Tech Mono" || !bubble.Visible) throw new InvalidOperationException("Lecture automatique/bulle non raccordée.");
                string diagnosticDirectory = Path.Combine(AppContext.BaseDirectory, "diagnostics"); Directory.CreateDirectory(diagnosticDirectory);
                using (Bitmap? image = bubble.Snapshot()) image?.Save(Path.Combine(diagnosticDirectory, "spoken-reply-bubble.png"), ImageFormat.Png);
                steps.Add(new { check = "deployed_spoken_reply_and_bubble", passed = true, font = bubble.FontName, pagesPlayed = muted.Calls, outputMutedForTest = true });
                if (muted.SampleRate != 22050 || muted.ProgressUpdates < 2) throw new InvalidOperationException("La voix Siwis et son affichage progressif ne sont pas actifs.");
                steps.Add(new { check = "deployed_neural_voice_progress", passed = true, muted.SampleRate, muted.ProgressUpdates, estimatedWordTiming = true });
                LocalVoice female = windowsVoice.GetVoices().FirstOrDefault() ?? throw new InvalidOperationException("Voix française Windows absente.");
                const string phrase = "Bonjour Iris, peux-tu me répondre en français ?";
                using PcmAudio fixture = await windowsVoice.SynthesizeAsync(phrase, new(female.Id), timeout.Token);
                var capture = new FixtureCapture(fixture);
                using var voice = new VoiceWindow(capture, new[] { new WhisperRecognitionProvider(engineRoot) }, synthesis, muted, new(), _ => { }, chat.SetDraft, emergency, emergency.Stop,
                    beforeCapture: reader.StopAsync, readWithBubble: (text, settings, progress, token) => reader.SpeakAsync(text, settings, progress, token)) { Opacity = 0, ShowInTaskbar = false };
                voice.Show(host);
                await UntilAsync(() => Find<ComboBox>(voice, "Microphone").Items.Count == 1 && Find<Button>(voice, "SaveVoice").Enabled, timeout.Token);
                if (Find<CheckBox>(voice, "MicrophoneConsent").Checked) throw new InvalidOperationException("Consentement micro implicite.");
                Find<CheckBox>(voice, "MicrophoneConsent").Checked = true;
                Task recording = voice.BeginCaptureAsync(); voice.EndCapture();
                await recording.WaitAsync(timeout.Token);
                string recognized = Find<TextBox>(voice, "Transcript").Text;
                WordErrorResult metric = WordErrorMetrics.Compare(phrase, recognized);
                if (recognized.Length == 0 || metric.Rate > 0.5) throw new InvalidOperationException("Transcription de fixture en échec : " + Find<Label>(voice, "Status").Text);
                steps.Add(new { check = "deployed_whisper_ui", passed = true, syntheticOnly = true, text = recognized, metric });
                Find<Button>(voice, "PlaceInChat").PerformClick();
                if (composer.Text != recognized) throw new InvalidOperationException("Transfert vocal vers le brouillon impossible.");
                steps.Add(new { check = "deployed_voice_to_chat_draft", passed = true });
                await voice.StopAndCloseAsync(); chat.Close(); await inference.StopAsync();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                steps.Add(new { check = "deployed_child_shutdown", passed = true });
                code = 0;
            }
            catch (Exception error) { failure = error.ToString(); }
            finally
            {
                emergency.Stop();
                string directory = Path.Combine(AppContext.BaseDirectory, "diagnostics"); Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "self-check.json"), JsonSerializer.Serialize(new
                { at = DateTimeOffset.UtcNow, version = typeof(Program).Assembly.GetName().Version?.ToString(), applicationDirectory = AppContext.BaseDirectory, engineRoot,
                    passed = code == 0, microphoneUsed = false, speakersUsed = false, steps, failure }, new JsonSerializerOptions { WriteIndented = true }));
                host.Close();
            }
        };
        Application.Run(host); return code;
    }
    private sealed class FixtureCapture(PcmAudio fixture) : IAudioCapture
    {
        public IReadOnlyList<AudioInputDevice> GetDevices() => new[] { new AudioInputDevice("fixture-only", "Audio synthétique de diagnostic — aucun micro") };
        public async Task<PcmAudio> CaptureAsync(string id, CaptureSettings settings, IProgress<AudioLevel>? level, CancellationToken released, CancellationToken token)
        {
            while (!released.IsCancellationRequested) await Task.Delay(10, token);
            token.ThrowIfCancellationRequested(); return new PcmAudio(fixture.Data.Span);
        }
    }
    private sealed class MutedPlayback(Action check) : IAudioPlayback
    {
        public int Calls;
        public int SampleRate, ProgressUpdates;
        public async Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? level, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); check(); Calls++; SampleRate = audio.SampleRateHz;
            for (int i = 0; i <= 100; i++) { level?.Report(new(0, 0, false, TimeSpan.FromTicks(audio.Duration.Ticks * i / 100), true)); ProgressUpdates++; await Task.Delay(2, token); }
        }
    }

    public static int RunSpeechCheck()
    {
        int code = 1;
        using var host = new Form { Opacity = 0, ShowInTaskbar = false };
        host.Shown += async (_, _) =>
        {
            string? failure = null;
            int textUpdates = 0, sampleRate = 0;
            bool devicePosition = false;
            using var emergency = new EmergencyStopService();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                using var bubble = new SpeechBubbleWindow(Path.Combine(AppContext.BaseDirectory, "assets"), host, () => host.Bounds);
                var windows = new WindowsTextToSpeechProvider();
                string engineRoot = EngineAssets.Resolve(AppContext.BaseDirectory, LocalSettingsStore.DefaultDirectory);
                var synthesis = new CompositeSpeechProvider(new PiperSynthesisProvider(engineRoot, windows), windows);
                var observed = new ObservedPlayback(bubble);
                using var reader = new ReplySpeechController(synthesis, observed, bubble, () => new(), _ => { }, emergency.Stop, emergency.Token);
                await reader.SpeakAsync("Bonjour. Je suis IRIS. Ma réponse s’affiche maintenant dans la bulle.", token: timeout.Token);
                textUpdates = observed.TextUpdates; sampleRate = observed.SampleRate; devicePosition = observed.DevicePosition;
                if (sampleRate != 22050 || textUpdates < 3) throw new InvalidOperationException("Lecture Siwis ou affichage progressif non confirmé.");
                code = 0;
            }
            catch (Exception error) { failure = error.ToString(); }
            finally
            {
                emergency.Stop();
                string directory = Path.Combine(AppContext.BaseDirectory, "diagnostics"); Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "speech-check.json"), JsonSerializer.Serialize(new
                    { at = DateTimeOffset.UtcNow, passed = code == 0, microphoneUsed = false, speakersUsed = true, textUpdates, sampleRate, devicePosition, estimatedWordTiming = true, failure }, new JsonSerializerOptions { WriteIndented = true }));
                host.Close();
            }
        };
        Application.Run(host); return code;
    }
    private sealed class ObservedPlayback(SpeechBubbleWindow bubble) : IAudioPlayback
    {
        public int TextUpdates, SampleRate;
        public bool DevicePosition;
        public async Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? progress, CancellationToken token)
        {
            SampleRate = audio.SampleRateHz; string previous = "";
            var observed = new Progress<AudioLevel>(level =>
            {
                DevicePosition |= level.DevicePosition;
                if (bubble.DisplayedText != previous) { previous = bubble.DisplayedText; TextUpdates++; }
                progress?.Report(level);
            });
            await new WinMmAudioPlayback().PlayAsync(audio, observed, token);
        }
    }
}
