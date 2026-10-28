using System;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Voice;
using IRIS.Core.Services;

namespace IRIS.Desktop.Speech;

internal sealed class ReplySpeechController : IDisposable
{
    private readonly ITextToSpeechProvider synthesis;
    private readonly IAudioPlayback playback;
    private readonly ISpeechBubble bubble;
    private readonly Func<VoicePreferences> preferences;
    private readonly Action<bool> speaking;
    private readonly Action emergencyStop;
    private readonly CancellationToken lifetime;
    private CancellationTokenSource? active;
    private Task running = Task.CompletedTask;
    private int generation;
    private bool disposed, driverFault;
    public bool IsBusy => !running.IsCompleted;
    public event Action? StateChanged;

    public ReplySpeechController(ITextToSpeechProvider synthesis, IAudioPlayback playback, ISpeechBubble bubble,
        Func<VoicePreferences> preferences, Action<bool> speaking, Action emergencyStop, CancellationToken lifetime)
    {
        this.synthesis = synthesis; this.playback = playback; this.bubble = bubble; this.preferences = preferences;
        this.speaking = speaking; this.emergencyStop = emergencyStop; this.lifetime = lifetime;
    }
    public Task SpeakAsync(string text, SpeechSynthesisSettings? settings = null, IProgress<AudioLevel>? progress = null, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (driverFault) throw new AudioDriverShutdownException("Arrêt du pilote non confirmé. Redémarrez IRIS avant une autre lecture.");
        Task previous = running;
        active?.Cancel(); bubble.Hide();
        int ticket = ++generation;
        var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime, token); active = request;
        Task current = RunAsync(text, settings, progress, request, previous, ticket); running = current;
        StateChanged?.Invoke(); _ = NotifyCompletionAsync(current); return current;
    }
    private async Task RunAsync(string text, SpeechSynthesisSettings? settings, IProgress<AudioLevel>? progress, CancellationTokenSource request, Task previous, int ticket)
    {
        try
        {
            text = text.Trim();
            try { await previous; } catch (Exception) { if (driverFault) throw new AudioDriverShutdownException("Le pilote de sortie audio ne confirme pas son arrêt."); }
            request.Token.ThrowIfCancellationRequested();
            if (driverFault) throw new AudioDriverShutdownException("Le pilote de sortie audio ne confirme pas son arrêt.");
            var pages = bubble.Paginate(text);
            if (settings == null)
            {
                VoicePreferences saved = preferences(); saved.Validate();
                var voices = synthesis.GetVoices();
                LocalVoice? chosen = null;
                foreach (LocalVoice voice in voices) if (voice.Id == saved.VoiceId) chosen = voice;
                if (saved.PreferNatural) foreach (LocalVoice voice in voices) if (voice.Id.StartsWith("piper-", StringComparison.Ordinal)) { chosen = voice; break; }
                chosen ??= voices.Count > 0 ? voices[0] : null;
                if (chosen == null) throw new InvalidOperationException("Aucune voix féminine française Windows disponible. Choisissez une voix dans la fenêtre Voix.");
                settings = new(chosen.Id, saved.Volume, saved.Rate);
            }
            int pageIndex = 0, pageStart = 0, displayedEnd = -1;
            void Reveal(int end)
            {
                end = Math.Clamp(end, 0, text.Length);
                if (end <= displayedEnd) return;
                while (pageIndex + 1 < pages.Count && end > pageStart + pages[pageIndex].Length) { pageStart += pages[pageIndex].Length; pageIndex++; }
                bubble.ShowPage(pages[pageIndex][..Math.Clamp(end - pageStart, 0, pages[pageIndex].Length)], pageIndex, pages.Count);
                displayedEnd = end;
            }
            foreach (SpeechChunk chunk in SpeechTextFlow.Chunks(text))
            {
                request.Token.ThrowIfCancellationRequested();
                using TimedSpeech speech = synthesis is ITimedTextToSpeechProvider timed
                    ? await timed.SynthesizeTimedAsync(chunk.Text, settings, request.Token)
                    : await EstimateAsync(chunk.Text, settings, request.Token);
                request.Token.ThrowIfCancellationRequested();
                var timeline = new SpeechProgressTimeline(chunk.Text, speech.Words.Count > 0 ? speech.Words : SpeechTiming.Estimate(chunk.Text, speech.Audio.Duration), speech.Audio.Duration);
                bool playing = true;
                var synchronized = new Progress<AudioLevel>(level =>
                {
                    if (!playing || disposed || ticket != generation || request.IsCancellationRequested) return;
                    Reveal(chunk.Start + timeline.At(level.Elapsed)); progress?.Report(level);
                });
                Reveal(chunk.Start);
                speaking(true);
                try { await playback.PlayAsync(speech.Audio, synchronized, request.Token); request.Token.ThrowIfCancellationRequested(); Reveal(chunk.Start + chunk.Text.Length); }
                finally { playing = false; speaking(false); }
            }
            request.Token.ThrowIfCancellationRequested();
            if (ticket == generation && !disposed) bubble.Finish();
        }
        catch (AudioDriverShutdownException) { driverFault = true; emergencyStop(); throw; }
        finally
        {
            if (ticket == generation)
            {
                if (request.IsCancellationRequested || driverFault) bubble.Hide();
            }
            if (ReferenceEquals(active, request)) active = null;
            request.Dispose();
        }
    }
    private async Task<TimedSpeech> EstimateAsync(string text, SpeechSynthesisSettings settings, CancellationToken token)
    {
        PcmAudio audio = await synthesis.SynthesizeAsync(text, settings, token);
        return new(audio, SpeechTiming.Estimate(text, audio.Duration), true);
    }
    private async Task NotifyCompletionAsync(Task task)
    {
        try { await task; } catch (Exception) { /* The caller observes the original task. */ }
        if (ReferenceEquals(running, task)) running = Task.CompletedTask;
        if (!disposed) StateChanged?.Invoke();
    }
    public void Stop() { generation++; var request = active; active = null; request?.Cancel(); if (!disposed) bubble.Hide(); StateChanged?.Invoke(); }
    public async Task StopAsync()
    {
        Task pending = running; Stop();
        try { await pending; } catch (OperationCanceledException) { }
        if (driverFault) throw new AudioDriverShutdownException("Le pilote de sortie audio ne confirme pas son arrêt.");
    }
    public void Dispose() { if (disposed) return; Stop(); disposed = true; }
}
