using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;

namespace IRIS.Voice;

public sealed class WindowsSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    public string Id => "windows-fr-FR";
    public string Name => "Windows français hors ligne (secours)";
    public bool IsAvailable => SpeechRecognitionEngine.InstalledRecognizers().Any(r => r.Culture.Name == "fr-FR");
    public Task<SpeechRecognitionResult> RecognizeAsync(PcmAudio audio, CancellationToken cancellationToken)
    {
        if (audio.SampleRateHz != 16000 || audio.Data.Length > PcmAudio.MaximumCaptureBytes) throw new ArgumentException("Reconnaissance limitée à 30 secondes à 16 kHz.");
        if (audio.Data.Length == 0) return Task.FromResult(new SpeechRecognitionResult("", null, Id));
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecognizerInfo selected = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.Name == "fr-FR")
                ?? throw new InvalidOperationException("Aucun reconnaisseur français Windows hors ligne installé.");
            using var recognizer = new SpeechRecognitionEngine(selected);
            using Stream input = audio.OpenRead();
            recognizer.SetInputToAudioStream(input, new SpeechAudioFormatInfo(PcmAudio.SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(3);
            recognizer.EndSilenceTimeout = TimeSpan.FromMilliseconds(500);
            recognizer.BabbleTimeout = TimeSpan.FromSeconds(30);
            recognizer.LoadGrammar(new DictationGrammar());
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pieces = new List<(string Text, double Confidence)>();
            recognizer.SpeechRecognized += (_, e) => { lock (pieces) pieces.Add((e.Result.Text, e.Result.Confidence)); };
            recognizer.RecognizeCompleted += (_, e) =>
            {
                if (e.Error != null) completed.TrySetException(new InvalidOperationException("Le reconnaisseur Windows a échoué.", e.Error));
                else if (e.Cancelled) completed.TrySetCanceled(); else completed.TrySetResult(true);
            };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            using var registration = deadline.Token.Register(() => { try { recognizer.RecognizeAsyncCancel(); } catch (InvalidOperationException) { } });
            deadline.Token.ThrowIfCancellationRequested();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            await completed.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            lock (pieces)
                return new SpeechRecognitionResult(string.Join(" ", pieces.Select(p => p.Text)), pieces.Count == 0 ? null : pieces.Average(p => p.Confidence), Id);
        }, cancellationToken);
    }
}

public sealed class WindowsTextToSpeechProvider : ITimedTextToSpeechProvider
{
    public IReadOnlyList<LocalVoice> GetVoices()
    {
        using var synthesizer = new SpeechSynthesizer();
        return synthesizer.GetInstalledVoices().Where(v => v.Enabled && v.VoiceInfo.Culture.Name == "fr-FR" && v.VoiceInfo.Gender == VoiceGender.Female)
            .Select(v => new LocalVoice(v.VoiceInfo.Id, v.VoiceInfo.Name, v.VoiceInfo.Culture.Name, true)).ToArray();
    }
    public async Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken cancellationToken)
        => (await SynthesizeTimedAsync(text, settings, cancellationToken).ConfigureAwait(false)).Audio;
    public Task<TimedSpeech> SynthesizeTimedAsync(string text, SpeechSynthesisSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 700 || settings.Volume is < 0 or > 100 || settings.Rate is < -5 or > 5)
            throw new ArgumentException("Texte vocal de 1 à 700 caractères, volume 0–100 et vitesse -5 à +5 attendus.");
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var synthesizer = new SpeechSynthesizer();
            InstalledVoice voice = synthesizer.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Id == settings.VoiceId && v.VoiceInfo.Culture.Name == "fr-FR" && v.VoiceInfo.Gender == VoiceGender.Female)
                ?? throw new InvalidOperationException("Voix féminine française locale indisponible.");
            synthesizer.SelectVoice(voice.VoiceInfo.Name);
            synthesizer.Volume = settings.Volume; synthesizer.Rate = settings.Rate;
            using var output = new MemoryStream();
            synthesizer.SetOutputToAudioStream(output, new SpeechAudioFormatInfo(PcmAudio.SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var words = new List<SpeechWordBoundary>();
            synthesizer.SpeakProgress += (_, e) => { lock (words) words.Add(new(e.CharacterPosition, e.CharacterCount, e.AudioPosition)); };
            synthesizer.SpeakCompleted += (_, e) =>
            {
                if (e.Error != null) completion.TrySetException(new InvalidOperationException("La synthèse vocale Windows a échoué.", e.Error));
                else if (e.Cancelled) completion.TrySetCanceled(); else completion.TrySetResult(true);
            };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            using var registration = deadline.Token.Register(() => { try { synthesizer.SpeakAsyncCancelAll(); } catch (ObjectDisposedException) { } });
            deadline.Token.ThrowIfCancellationRequested();
            synthesizer.SpeakAsync(text); // Plain text only: no SSML, URLs or external audio.
            await completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            lock (words) return new TimedSpeech(new PcmAudio(output.ToArray()), words.ToArray(), false);
        }, cancellationToken);
    }
}
