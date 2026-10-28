using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Infrastructure;
using IRIS.Infrastructure.Voice;
using IRIS.Voice;

namespace IRIS.VoiceTests;

internal static class Program
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--benchmark") await BenchmarkAsync();
            else if (args.Length == 1 && args[0] == "--quiet-regression") await QuietRegressionAsync();
            else if (args.Length == 1 && args[0] == "--compare-tts") await CompareVoicesAsync();
            else await ChecksAsync();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private sealed class Capture : IAudioCapture
    {
        public int Calls;
        public bool Silence;
        public IReadOnlyList<AudioInputDevice> GetDevices() => new[] { new AudioInputDevice("test", "Microphone simulé") };
        public async Task<PcmAudio> CaptureAsync(string deviceId, CaptureSettings settings, IProgress<AudioLevel>? progress, CancellationToken released, CancellationToken token)
        {
            Calls++;
            while (!released.IsCancellationRequested) await Task.Delay(10, token);
            token.ThrowIfCancellationRequested();
            var pcm = new byte[Silence ? 0 : 3200];
            for (int i = 0; i < pcm.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i), (short)(Math.Sin(i * 0.1) * 4000));
            return new PcmAudio(pcm);
        }
    }
    private sealed class Recognition : ISpeechRecognitionProvider
    {
        public int Calls;
        public string Id => "test";
        public string Name => "Simulé";
        public bool IsAvailable => true;
        public Task<SpeechRecognitionResult> RecognizeAsync(PcmAudio audio, CancellationToken token) { Calls++; return Task.FromResult(new SpeechRecognitionResult("Texte de test.", 0.8, Id)); }
    }
    private sealed class Synthesis : ITextToSpeechProvider
    {
        public PcmAudio? Last;
        public IReadOnlyList<LocalVoice> GetVoices() => new[] { new LocalVoice("test", "Voix simulée", "fr-FR", true) };
        public Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken token) { Last = new PcmAudio(new byte[3200]); return Task.FromResult(Last); }
    }
    private sealed class Playback : IAudioPlayback
    {
        public bool Started;
        public async Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? progress, CancellationToken token)
        { Started = true; await Task.Delay(Timeout.Infinite, token); }
    }
    private static async Task ChecksAsync()
    {
        var passed = new List<string>();
        void Pass(string name) { passed.Add("PASS " + name); }
        byte[] samples = new byte[32000];
        for (int index = 0; index < samples.Length; index += 2) BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(index), (short)(Math.Sin(index * 0.07) * 4000));
        using (var clip = new PcmAudio(samples))
        using (PcmAudio restored = PcmAudio.FromWave(clip.ToWave())) Require(restored.Data.Span.SequenceEqual(samples) && restored.Duration == TimeSpan.FromSeconds(1), "PCM WAV roundtrip failed");
        Pass("PCM/WAV roundtrip preserves format, samples and duration");
        using (var neuralClip = new PcmAudio(new byte[44100], 22050))
        {
            byte[] wave = neuralClip.ToWave();
            Require(neuralClip.Duration == TimeSpan.FromSeconds(1) && BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(24)) == 22050, "Neural sample rate changed playback speed");
            bool rejected = false;
            try { using var ignored = AudioProcessing.PrepareForRecognition(neuralClip, 0.005, out _); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "22kHz speech entered the 16kHz recognition pipeline");
        }
        Pass("neural PCM preserves 22050 Hz duration and WAV headers; recognition rejects an incompatible rate");
        using (var clip = new PcmAudio(samples))
        {
            byte[] malformed = clip.ToWave(); malformed[22] = 2;
            bool rejected = false; try { using var ignored = PcmAudio.FromWave(malformed); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Stereo WAV accepted");
            malformed = clip.ToWave(); Array.Resize(ref malformed, malformed.Length - 1);
            rejected = false; try { using var ignored = PcmAudio.FromWave(malformed); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Truncated WAV accepted");
        }
        Pass("malformed, truncated and unsupported audio is rejected");
        using (var silence = new PcmAudio(new byte[32000]))
        using (PcmAudio trimmed = AudioProcessing.TrimSilence(silence, 0.005)) Require(trimmed.Data.Length == 0, "Silence retained for recognition");
        Pass("silence does not trigger transcription");
        byte[] loud = new byte[4]; BinaryPrimitives.WriteInt16LittleEndian(loud, short.MaxValue); BinaryPrimitives.WriteInt16LittleEndian(loud.AsSpan(2), short.MinValue);
        AudioProcessing.ApplyGain(loud, 2);
        Require(BinaryPrimitives.ReadInt16LittleEndian(loud) == short.MaxValue && AudioProcessing.Measure(loud, default).Clipped, "Gain wrapped samples");
        Pass("input gain clamps samples and reports clipping");
        byte[] soft = new byte[32000];
        for (int i = 0; i < soft.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(soft.AsSpan(i), (short)(Math.Sin(i * 0.11) * 100));
        using (var quiet = new PcmAudio(soft))
        using (PcmAudio prepared = AudioProcessing.PrepareForRecognition(quiet, 0.005, out SpeechSignalAnalysis analysis))
        {
            Require(prepared.Duration.TotalSeconds > 0.9 && analysis.RecognitionGain > 1 && !analysis.TooQuiet, "Quiet speech was discarded");
            Require(quiet.Data.Span.SequenceEqual(soft) && AudioProcessing.Measure(prepared.Data.Span, default).Peak <= 0.951, "Preprocessing mutated or clipped input");
        }
        Pass("quiet speech survives a high threshold and is amplified within the peak limit without changing the input");
        byte[] paused = new byte[160000]; soft.CopyTo(paused, 0); soft.CopyTo(paused, paused.Length - soft.Length);
        using (var pause = new PcmAudio(paused))
        using (PcmAudio prepared = AudioProcessing.PrepareForRecognition(pause, 0.005, out _)) Require(prepared.Duration.TotalSeconds > 4.9, "An internal hesitation cut off the second phrase");
        Pass("long internal pauses are retained instead of cutting the next words");
        byte[] dc = new byte[32000];
        for (int i = 0; i < dc.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(dc.AsSpan(i), 500);
        using (var offset = new PcmAudio(dc))
        using (PcmAudio prepared = AudioProcessing.PrepareForRecognition(offset, 0.005, out SpeechSignalAnalysis analysis)) Require(prepared.Data.Length == 0 && analysis.TooQuiet, "DC-only audio was treated as speech");
        Pass("DC offset without speech is rejected before recognition");
        WordErrorResult metric = WordErrorMetrics.Compare("bonjour le monde", "bonjour un monde nouveau");
        Require(metric.Substitutions == 1 && metric.Insertions == 1 && metric.Deletions == 0 && Math.Abs(metric.Rate!.Value - 2.0 / 3) < 0.001, "WER edit distance failed");
        Require(WordErrorMetrics.Compare("Bonjour, IRIS !", "bonjour iris").Rate == 0 && WordErrorMetrics.Compare("", "bruit").Rate == null, "WER normalization or empty reference failed");
        Pass("WER counts real substitutions, deletions and insertions with explicit normalization");
        var capture = new Capture(); var recognition = new Recognition(); var synthesis = new Synthesis(); var playback = new Playback();
        var workflow = new VoiceWorkflow(capture, synthesis, playback);
        bool denied = false;
        try { await workflow.CaptureAndRecognizeAsync(false, "test", new(), recognition, null, () => { }, default, default); } catch (InvalidOperationException) { denied = true; }
        Require(denied && capture.Calls == 0, "Microphone used without consent");
        Pass("microphone consent is enforced before opening a capture source");
        using (var release = new CancellationTokenSource())
        {
            bool closed = false;
            Task<SpeechRecognitionResult> pending = workflow.CaptureAndRecognizeAsync(true, "test", new(), recognition, null, () => closed = true, release.Token, default);
            release.Cancel();
            Require((await pending.WaitAsync(TimeSpan.FromSeconds(2))).Text == "Texte de test." && closed && recognition.Calls == 1, "Push-to-talk release failed");
        }
        Pass("push-to-talk release closes capture before recognizing the clip");
        using (var cancel = new CancellationTokenSource())
        {
            Task<SpeechRecognitionResult> pending = workflow.CaptureAndRecognizeAsync(true, "test", new(), recognition, null, () => { }, default, cancel.Token);
            cancel.Cancel();
            try { await pending; throw new InvalidOperationException("Cancelled capture returned text"); } catch (OperationCanceledException) { }
            Require(recognition.Calls == 1, "Cancelled capture reached recognition");
        }
        Pass("capture cancellation drops the clip and prevents recognition");
        using (var release = new CancellationTokenSource())
        {
            capture.Silence = true; release.Cancel();
            Require((await workflow.CaptureAndRecognizeAsync(true, "test", new(), recognition, null, () => { }, release.Token, default)).Text.Length == 0 && recognition.Calls == 1, "Silence sent to engine");
        }
        Pass("an empty capture never calls the recognizer");
        using (var cancel = new CancellationTokenSource())
        {
            Task pending = workflow.SpeakAsync("Texte synthétique", new("test"), null, cancel.Token);
            while (!playback.Started) await Task.Yield();
            cancel.Cancel(); try { await pending; throw new InvalidOperationException("Playback not cancelled"); } catch (OperationCanceledException) { }
            bool disposed = false; try { _ = synthesis.Last!.Data; } catch (ObjectDisposedException) { disposed = true; }
            Require(disposed, "Synthesis buffers retained after cancellation");
        }
        Pass("playback cancellation disposes the synthesized audio buffer");
        foreach (string result in passed) Console.WriteLine(result);
        Console.WriteLine($"{passed.Count} contrôles vocaux réussis, sans vrai microphone ni haut-parleur.");
    }
    private static async Task BenchmarkAsync()
    {
        var synth = new WindowsTextToSpeechProvider();
        LocalVoice voice = synth.GetVoices().FirstOrDefault() ?? throw new InvalidOperationException("Voix féminine française Windows absente.");
        ISpeechRecognitionProvider[] providers = { new WindowsSpeechRecognitionProvider(), new WhisperRecognitionProvider(LocalSettingsStore.DefaultDirectory) };
        string[] phrases = {
            "Bonjour Iris, peux-tu me répondre en français ?",
            "Je préfère une voix calme et une réponse assez courte.",
            "Rappelle-moi de prendre mes affaires demain à dix-sept heures.",
            "Ouvre les paramètres du microphone et vérifie le niveau sonore.",
            "Je joue à Minecraft et je travaille avec Visual Studio Code.",
            "Le processeur est un Ryzen et la carte graphique utilise Vulkan."
        };
        var results = new List<object>();
        int failures = 0;
        string directory = Path.GetFullPath("out/speech"); Directory.CreateDirectory(directory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        Console.WriteLine("Corpus synthétique produit par " + voice.Name + ". Aucun microphone ou haut-parleur utilisé.");
        for (int index = 0; index < phrases.Length; index++)
        {
            using PcmAudio audio = await synth.SynthesizeAsync(phrases[index], new(voice.Id, 80, 0), timeout.Token);
            if (index == 0) await File.WriteAllBytesAsync(Path.Combine(directory, "hortense-exemple.wav"), audio.ToWave(), timeout.Token);
            foreach (ISpeechRecognitionProvider provider in providers)
            {
                if (!provider.IsAvailable) { failures++; results.Add(new { reference = phrases[index], provider = provider.Id, error = "Indisponible" }); continue; }
                var watch = Stopwatch.StartNew();
                try
                {
                    SpeechRecognitionResult recognized = await provider.RecognizeAsync(audio, timeout.Token);
                    if (string.IsNullOrWhiteSpace(recognized.Text)) throw new InvalidDataException("Transcription vide du corpus synthétique.");
                    WordErrorResult metrics = WordErrorMetrics.Compare(phrases[index], recognized.Text);
                    results.Add(new { reference = phrases[index], provider = provider.Id, recognized.Text, recognized.Confidence, metrics, seconds = watch.Elapsed.TotalSeconds, audioSeconds = audio.Duration.TotalSeconds });
                    Console.WriteLine($"{index + 1}/{phrases.Length} {provider.Id} : WER {metrics.Rate:P1} ; {watch.Elapsed.TotalSeconds:F2} s ; {recognized.Text}");
                }
                catch (Exception error) { failures++; results.Add(new { reference = phrases[index], provider = provider.Id, error = error.Message, seconds = watch.Elapsed.TotalSeconds }); Console.WriteLine(provider.Id + " : " + error.Message); }
            }
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "benchmark.json"), JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, voice, syntheticOnly = true, microphoneUsed = false, speakerUsed = false, results,
            note = "Corpus TTS court, sans bruit ni locuteur humain ; aucune conclusion générale sur le français ou le naturel de la voix." }, new JsonSerializerOptions { WriteIndented = true }));
        Require(failures == 0, "Comparaison incomplète : moteur indisponible, transcription vide ou erreur ; consultez benchmark.json.");
        using PcmAudio cancellationClip = await synth.SynthesizeAsync(phrases[0], new(voice.Id), timeout.Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var existing = Process.GetProcessesByName("whisper-cli").Select(p => { using (p) return p.Id; }).ToHashSet();
        Task<SpeechRecognitionResult> pending = providers[1].RecognizeAsync(cancellationClip, cancellation.Token);
        Process? child = null;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (child == null && !pending.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(15))
            {
                foreach (Process candidate in Process.GetProcessesByName("whisper-cli"))
                    if (!existing.Contains(candidate.Id) && child == null) child = candidate; else candidate.Dispose();
                if (child == null) await Task.Delay(10, timeout.Token);
            }
            Require(child != null, "Processus Whisper absent pour vérifier l’annulation réelle.");
            cancellation.Cancel();
            bool interrupted = false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { interrupted = true; }
            Require(interrupted, "Whisper n’a pas propagé l’annulation.");
            await child!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine("PASS annulation réelle Whisper : processus terminé, sans microphone ni haut-parleur.");
        }
        finally
        {
            cancellation.Cancel();
            try { await pending; } catch (OperationCanceledException) { }
            child?.Dispose();
        }
    }

    private static async Task QuietRegressionAsync()
    {
        const string first = "Bonjour Iris.";
        const string second = "Je préfère une réponse en français.";
        var synth = new WindowsTextToSpeechProvider();
        LocalVoice voice = synth.GetVoices().First();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using PcmAudio a = await synth.SynthesizeAsync(first, new(voice.Id), deadline.Token);
        using PcmAudio b = await synth.SynthesizeAsync(second, new(voice.Id), deadline.Token);
        byte[] input = new byte[a.Data.Length + 64000 + b.Data.Length]; // two-second pause
        a.Data.Span.CopyTo(input); b.Data.Span.CopyTo(input.AsSpan(a.Data.Length + 64000));
        for (int i = 0; i < input.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(i), (short)(BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan(i)) * 0.025));
        using var quiet = new PcmAudio(input);
        using PcmAudio previous = AudioProcessing.TrimSilence(quiet, 0.005);
        using PcmAudio prepared = AudioProcessing.PrepareForRecognition(quiet, 0.005, out SpeechSignalAnalysis analysis);
        var whisper = new WhisperRecognitionProvider(LocalSettingsStore.DefaultDirectory);
        SpeechRecognitionResult recognized = await whisper.RecognizeAsync(prepared, deadline.Token);
        WordErrorResult metrics = WordErrorMetrics.Compare(first + " " + second, recognized.Text);
        string directory = Path.GetFullPath("out/speech"); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "quiet-regression.json"), JsonSerializer.Serialize(new
        { at = DateTimeOffset.UtcNow, syntheticOnly = true, microphoneUsed = false, reference = first + " " + second, previousBytes = previous.Data.Length, preparedBytes = prepared.Data.Length, analysis, recognized.Text, metrics }, new JsonSerializerOptions { WriteIndented = true }));
        Require(previous.Data.Length < prepared.Data.Length && recognized.Text.Length > 0 && metrics.Rate < 0.4, "Quiet synthetic speech regression failed; see quiet-regression.json.");
        Console.WriteLine($"PASS Whisper, parole synthétique faible et pause de 2 s : WER {metrics.Rate:P1}, ancien découpage {previous.Data.Length} octets, nouveau {prepared.Data.Length} ; {recognized.Text}");
    }
    private static async Task CompareVoicesAsync()
    {
        string folder = Path.GetFullPath("out/speech-comparison"); Directory.CreateDirectory(folder);
        var windows = new WindowsTextToSpeechProvider();
        var neural = new PiperSynthesisProvider(LocalSettingsStore.DefaultDirectory, windows);
        const string phrase = "Bonjour, je suis IRIS. Prenons le temps de discuter tranquillement. Le texte apparaît pendant que je parle.";
        var rows = new List<object>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        foreach (ITimedTextToSpeechProvider provider in new ITimedTextToSpeechProvider[] { windows, neural })
        {
            LocalVoice voice = provider.GetVoices().First(); var elapsed = Stopwatch.StartNew();
            using TimedSpeech speech = await provider.SynthesizeTimedAsync(phrase, new(voice.Id), deadline.Token);
            Require(speech.Audio.Data.Length > 0 && speech.Words.Count > 5 && speech.Words.Any(w => w.Position > TimeSpan.Zero), "Word timing metadata missing");
            Require(speech.Words.All(w => w.Start >= 0 && w.Start + w.Length <= phrase.Length && w.Position <= speech.Audio.Duration), "Invalid word timing metadata");
            string output = Path.Combine(folder, provider == windows ? "windows-hortense.wav" : "piper-siwis.wav");
            await File.WriteAllBytesAsync(output, speech.Audio.ToWave(), deadline.Token);
            rows.Add(new { voice, latencySeconds = elapsed.Elapsed.TotalSeconds, seconds = speech.Audio.Duration.TotalSeconds, rate = speech.Audio.SampleRateHz, speech.Estimated, words = speech.Words, output });
            Console.WriteLine($"PASS {voice.Name} · {speech.Audio.Duration.TotalSeconds:F2} s · {speech.Audio.SampleRateHz} Hz · {speech.Words.Count} repères · estimés={speech.Estimated} · synthèse {elapsed.Elapsed.TotalSeconds:F2} s");
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var existing = Process.GetProcessesByName("piper").Select(p => { using (p) return p.Id; }).ToHashSet();
        Task<TimedSpeech> pending = neural.SynthesizeTimedAsync(phrase, new(PiperSynthesisProvider.VoiceId), cancellation.Token);
        Process? child = null;
        try
        {
            var watch = Stopwatch.StartNew();
            while (child == null && !pending.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                foreach (Process candidate in Process.GetProcessesByName("piper"))
                    if (!existing.Contains(candidate.Id) && child == null) child = candidate; else candidate.Dispose();
                if (child == null) await Task.Delay(10, deadline.Token);
            }
            Require(child != null, "Piper process unavailable for cancellation check");
            cancellation.Cancel(); bool interrupted = false;
            try { using TimedSpeech unexpected = await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { interrupted = true; }
            Require(interrupted, "Piper ignored cancellation");
            await child!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine("PASS annulation réelle Piper : processus terminé, sans microphone ni haut-parleur.");
        }
        finally
        {
            cancellation.Cancel();
            try { using TimedSpeech completed = await pending; } catch (OperationCanceledException) { }
            child?.Dispose();
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "comparison.json"), JsonSerializer.Serialize(new { microphoneUsed = false, speakersUsed = false, phrase, rows, cancellationPassed = true }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
