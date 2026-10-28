using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.AI.Conversation;
using IRIS.AI.Inference;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Desktop;
using IRIS.Desktop.Speech;
using IRIS.Voice;

namespace IRIS.DesktopTests;

internal static class SpeechChecks
{
    private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    private static T Find<T>(Control form, string name) where T : Control => (T)form.Controls.Find(name, true).Single();
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
    private sealed class Bubble(BubbleArtwork artwork) : ISpeechBubble
    {
        public bool Visible;
        public bool Finished;
        public string? Text;
        public readonly List<string> Shown = new();
        public readonly Dictionary<int, string> Pages = new();
        public IReadOnlyList<string> Paginate(string text) => artwork.Paginate(text);
        public void ShowPage(string text, int page, int total) { Visible = true; Text = text; Shown.Add(text); Pages[page] = text; }
        public void Hide() { Visible = false; Text = null; }
        public void Finish() => Finished = true;
    }
    private sealed class Synth : ITextToSpeechProvider
    {
        public readonly List<string> Texts = new();
        public readonly List<PcmAudio> Clips = new();
        public TaskCompletionSource<bool>? Ready;
        public IReadOnlyList<LocalVoice> GetVoices() => new[] { new LocalVoice("fr-test", "Français test", "fr-FR", true) };
        public async Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken token)
        {
            Texts.Add(text); if (Ready != null) await Ready.Task.WaitAsync(token);
            var audio = new PcmAudio(new byte[3200]); Clips.Add(audio); return audio;
        }
    }
    private sealed class Playback : IAudioPlayback
    {
        public int Calls, Active, MaximumActive;
        public bool Hold, FailDriver;
        public Action? OnPlay;
        public async Task PlayAsync(PcmAudio clip, IProgress<AudioLevel>? progress, CancellationToken token)
        {
            Calls++; Active++; MaximumActive = Math.Max(Active, MaximumActive); OnPlay?.Invoke();
            try
            {
                if (FailDriver) throw new AudioDriverShutdownException("Pilote de test non arrêté.");
                for (int i = 0; i <= 100; i++)
                { progress?.Report(new AudioLevel(0, 0, false, TimeSpan.FromTicks(clip.Duration.Ticks * i / 100), true)); await Task.Delay(1, token); }
                if (Hold) await Task.Delay(Timeout.Infinite, token);
            }
            finally { Active--; }
        }
    }
    private sealed class Inference : ILocalInferenceService
    {
        public bool IsReady { get; private set; }
        public Task StartAsync(bool cpu, CancellationToken token) { IsReady = true; return Task.CompletedTask; }
        public Task StopAsync() { IsReady = false; return Task.CompletedTask; }
        public Task<string> GenerateAsync(IReadOnlyList<ChatEntry> context, GenerationSettings settings, CancellationToken token) => Task.FromResult("Bonjour, je peux maintenant vous répondre à voix haute.");
    }
    private sealed class Capture(Action check) : IAudioCapture
    {
        public int Calls;
        public IReadOnlyList<AudioInputDevice> GetDevices() => new[] { new AudioInputDevice("fixture", "Audio simulé") };
        public async Task<PcmAudio> CaptureAsync(string id, CaptureSettings settings, IProgress<AudioLevel>? level, CancellationToken released, CancellationToken token)
        {
            check(); Calls++;
            while (!released.IsCancellationRequested) await Task.Delay(10, token);
            byte[] samples = new byte[3200];
            for (int i = 0; i < samples.Length; i += 2) System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(i), (short)(Math.Sin(i * 0.1) * 4000));
            return new PcmAudio(samples);
        }
    }
    private sealed class Recognition : ISpeechRecognitionProvider
    {
        public string Id => "fixture"; public string Name => "Reconnaissance de test"; public bool IsAvailable => true;
        public Task<SpeechRecognitionResult> RecognizeAsync(PcmAudio audio, CancellationToken token) => Task.FromResult(new SpeechRecognitionResult("Parole de test.", 1, Id));
    }
    public static async Task<IReadOnlyList<string>> RunAsync(Form owner, string output)
    {
        var results = new List<string>();
        string assets = Path.Combine(AppContext.BaseDirectory, "assets");
        using var artwork = new BubbleArtwork(assets);
        Require(artwork.Size.Width == 560, "Bubble is no longer compact");
        const string timedText = "Bonjour tout le monde.";
        var timeline = new SpeechProgressTimeline(timedText, new[] { new SpeechWordBoundary(0, 7, TimeSpan.FromMilliseconds(100)), new SpeechWordBoundary(8, 4, TimeSpan.FromMilliseconds(400)), new SpeechWordBoundary(13, 2, TimeSpan.FromMilliseconds(600)), new SpeechWordBoundary(16, 6, TimeSpan.FromMilliseconds(800)) }, TimeSpan.FromSeconds(1));
        Require(timeline.At(TimeSpan.Zero) == 0 && timeline.At(TimeSpan.FromMilliseconds(200)) == 7 && timeline.At(TimeSpan.FromMilliseconds(500)) == 12 && timeline.At(TimeSpan.FromMilliseconds(900)) == timedText.Length, "Text did not follow word timestamps");
        var unicode = new SpeechProgressTimeline("🙂e\u0301", new[] { new SpeechWordBoundary(0, 1, TimeSpan.Zero), new SpeechWordBoundary(2, 1, TimeSpan.FromSeconds(1)) }, TimeSpan.FromSeconds(2));
        Require(unicode.At(TimeSpan.Zero) == 2 && unicode.At(TimeSpan.FromSeconds(1)) == 4, "Progress split Unicode graphemes");
        results.Add("compact bubble and progressive text follow word timestamps without splitting Unicode graphemes");
        Require(artwork.FontName == "Share Tech Mono", "Asset font not loaded privately: " + artwork.FontName);
        string longText = string.Join(" ", Enumerable.Repeat("Bonjour, je suis IRIS. Voici une réponse française complète avec des accents : éèàçœ et un emoji 🙂.", 30));
        IReadOnlyList<string> pages = artwork.Paginate(longText);
        Require(pages.Count > 1 && string.Concat(pages) == longText && pages.All(p => p.Length <= 600 && artwork.Fits(p)), "Bubble lost text or overflowed its interior");
        Require(artwork.Paginate(new string('x', 1500)).All(p => p.Length <= 600), "Long unbroken token rejected");
        using (Bitmap frame = artwork.Render("Bonjour, je suis IRIS. Je peux maintenant vous répondre à voix haute.", 0, 1))
        {
            Require(frame.GetPixel(0, 0).A == 0, "Bubble transparency flattened"); frame.Save(Path.Combine(output, "speech-bubble.png"), ImageFormat.Png);
        }
        for (int i = 0; i < Math.Min(3, pages.Count); i++) using (Bitmap frame = artwork.Render(pages[i], i, pages.Count)) frame.Save(Path.Combine(output, "speech-bubble-page-" + i + ".png"), ImageFormat.Png);
        results.Add("supplied bubble and Share Tech Mono render with alpha; pagination fits and preserves all text including Unicode");
        var bubble = new Bubble(artwork); var synth = new Synth(); var playback = new Playback(); var talking = new List<bool>();
        using var emergency = new EmergencyStopService();
        using (var speech = new ReplySpeechController(synth, playback, bubble, () => new(), talking.Add, emergency.Stop, emergency.Token))
        {
            playback.OnPlay = () => Require(bubble.Visible, "Audio began without the bubble");
            synth.Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task preparing = speech.SpeakAsync("Préparation de la voix.");
            Require(!bubble.Visible && playback.Calls == 0 && talking.Count == 0, "Bubble/talk appeared before audio was ready");
            synth.Ready.SetResult(true); await preparing; synth.Ready = null;
            synth.Texts.Clear(); bubble.Shown.Clear(); bubble.Pages.Clear(); talking.Clear(); playback.Calls = 0;
            await speech.SpeakAsync(longText);
            Require(string.Concat(synth.Texts) == longText && playback.Calls == SpeechTextFlow.Chunks(longText).Count && playback.Calls < pages.Count && bubble.Finished, "Audio was split by visual pages or lost text");
            Require(bubble.Pages.Count == pages.Count && bubble.Pages.All(p => pages[p.Key].StartsWith(p.Value, StringComparison.Ordinal)) && bubble.Shown.All(artwork.Fits) && bubble.Shown.Any(p => p.Length == 0) && bubble.Shown.Any(p => p.Length > 0 && p.Length < pages[0].Length), "Progressive text did not visit/fill the display pages");
            Require(talking.Count == playback.Calls * 2 && talking.Where((value, index) => value != (index % 2 == 0)).Count() == 0, "Talk state not bound to actual playback");
            bool freed = false; try { _ = synth.Clips[0].Data; } catch (ObjectDisposedException) { freed = true; } Require(freed, "Completed speech audio retained");
            playback.Hold = true;
            Task old = speech.SpeakAsync("Ancienne réponse."); await UntilAsync(() => playback.Active == 1);
            int before = playback.Calls;
            Task latest = speech.SpeakAsync("Nouvelle réponse.");
            await UntilAsync(() => playback.Calls == before + 1);
            await UntilAsync(() => bubble.Text == "Nouvelle réponse.");
            Require(playback.MaximumActive == 1, "Two voices played concurrently");
            await speech.StopAsync();
            try { await old; } catch (OperationCanceledException) { }
            try { await latest; } catch (OperationCanceledException) { }
            Require(!bubble.Visible && playback.Active == 0, "Stop did not hide bubble and drain output");
            playback.Hold = false; await speech.SpeakAsync("Reprise après arrêt.");
            Require(bubble.Text == "Reprise après arrêt.", "Speech could not restart after cancellation");
        }
        results.Add("text reveals progressively; fluent audio chunks span visual pages; replacement and stop drain one playback");

        var chatBubble = new Bubble(artwork); var chatSynth = new Synth(); var chatPlayback = new Playback { Hold = true };
        using (var reader = new ReplySpeechController(chatSynth, chatPlayback, chatBubble, () => new(), _ => { }, emergency.Stop, emergency.Token))
        using (var chat = new ConversationWindow(new Inference(), () => new PersonalityProfile(), emergency, emergency.Stop, speech: reader) { Opacity = 0, ShowInTaskbar = false })
        {
            chat.Show(owner); Find<Button>(chat, "Load").PerformClick(); await UntilAsync(() => Find<Button>(chat, "Send").Enabled);
            Require(Find<CheckBox>(chat, "ReadReplies").Checked, "User-requested automatic reading not enabled");
            Find<TextBox>(chat, "Input").Text = "Bonjour"; Find<Button>(chat, "Send").PerformClick();
            await UntilAsync(() => chatPlayback.Active == 1);
            Require(chatBubble.Visible && Find<RichTextBox>(chat, "Transcript").Text.Contains(chatBubble.Text!, StringComparison.Ordinal), "Chat response did not reach speech/bubble");
            Find<Button>(chat, "Cancel").PerformClick(); await UntilAsync(() => !reader.IsBusy);
            Require(!chatBubble.Visible, "Chat interruption left the bubble visible");
            Find<CheckBox>(chat, "ReadReplies").Checked = false; int calls = chatPlayback.Calls;
            Find<TextBox>(chat, "Input").Text = "Réponse silencieuse"; Find<Button>(chat, "Send").PerformClick();
            await UntilAsync(() => Find<Button>(chat, "Send").Enabled);
            Require(chatPlayback.Calls == calls, "Muted chat still spoke");
            Find<Button>(chat, "ReplayReply").PerformClick(); await UntilAsync(() => chatPlayback.Active == 1);
            chat.InvalidateMemory(); await UntilAsync(() => !reader.IsBusy);
            Require(!chatBubble.Visible && !Find<Button>(chat, "ReplayReply").Enabled, "Deleted memory left a stale spoken reply");
            chat.Close();
        }
        results.Add("chat responses automatically speak in the bubble; mute, replay, interruption and memory invalidation are connected");

        var duplexBubble = new Bubble(artwork); var duplexPlayback = new Playback { Hold = true };
        using (var reader = new ReplySpeechController(new Synth(), duplexPlayback, duplexBubble, () => new(), _ => { }, emergency.Stop, emergency.Token))
        {
            Task oldSpeech = reader.SpeakAsync("Lecture avant la capture."); await UntilAsync(() => duplexPlayback.Active == 1);
            var capture = new Capture(() => Require(duplexPlayback.Active == 0 && !duplexBubble.Visible, "Microphone opened before output stopped"));
            using var voice = new VoiceWindow(capture, new[] { new Recognition() }, new Synth(), new Playback(), new(), _ => { }, _ => { }, emergency, emergency.Stop,
                beforeCapture: reader.StopAsync, readWithBubble: (text, settings, progress, token) => reader.SpeakAsync(text, settings, progress, token)) { Opacity = 0, ShowInTaskbar = false };
            voice.Show(owner); await UntilAsync(() => Find<ComboBox>(voice, "Microphone").Items.Count == 1 && Find<Button>(voice, "SaveVoice").Enabled);
            Find<CheckBox>(voice, "MicrophoneConsent").Checked = true;
            Task recording = voice.BeginCaptureAsync(); voice.EndCapture(); await recording;
            try { await oldSpeech; } catch (OperationCanceledException) { }
            Require(capture.Calls == 1, "Capture not started after draining audio");
            Find<Button>(voice, "TestVoice").PerformClick(); await UntilAsync(() => duplexPlayback.Active == 1);
            Require(duplexBubble.Visible, "Manual voice test bypassed the shared bubble");
            Find<Button>(voice, "CancelVoice").PerformClick(); await UntilAsync(() => Find<Button>(voice, "SaveVoice").Enabled);
            Require(!duplexBubble.Visible && duplexPlayback.Active == 0, "Manual speech cancellation left audio active");
            await voice.StopAndCloseAsync();
        }
        results.Add("push-to-talk drains spoken output before capture; manual voice reading uses the same interruptible bubble");

        var faultBubble = new Bubble(artwork);
        using (var driverStop = new EmergencyStopService())
        using (var faulty = new ReplySpeechController(new Synth(), new Playback { FailDriver = true }, faultBubble, () => new(), _ => { }, driverStop.Stop, driverStop.Token))
        {
            bool failed = false; try { await faulty.SpeakAsync("Test du pilote."); } catch (AudioDriverShutdownException) { failed = true; }
            Require(failed && driverStop.IsStopped && !faultBubble.Visible, "Unconfirmed driver shutdown did not fail closed");
        }
        results.Add("audio driver shutdown failure locks the session and closes the speech bubble");

        var work = new Rectangle(-1920, 0, 1920, 1080);
        Point position = SpeechBubbleWindow.Position(new(-100, 30, 100, 200), new(780, 260), work);
        Require(work.Contains(new Rectangle(position, new Size(780, 260))), "Bubble placed outside a negative-coordinate monitor");
        using (var window = new SpeechBubbleWindow(assets, owner, () => owner.Bounds))
        {
            window.ShowPage("Bonjour, voici la bulle IRIS avec la police fournie.", 0, 1);
            Require(window.Visible && window.FontName == "Share Tech Mono", "Native bubble/font unavailable"); window.Hide();
        }
        results.Add("native layered bubble uses the supplied font, stays inside the work area and can close without stealing focus");
        return results;
    }
}
