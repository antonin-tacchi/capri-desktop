using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.Core.Contracts;

public sealed record AudioInputDevice(string Id, string Name);
public sealed record LocalVoice(string Id, string Name, string Language, bool Female);
public sealed record CaptureSettings(double Gain = 1, double SpeechThreshold = 0.005);
public sealed record AudioLevel(double Rms, double Peak, bool Clipped, TimeSpan Elapsed, bool DevicePosition = false);
public sealed record SpeechSignalAnalysis(TimeSpan Duration, double Rms, double Peak, bool Clipped, double RecognitionGain, bool TooQuiet);
public sealed record SpeechRecognitionResult(string Text, double? Confidence, string Provider);
public sealed record SpeechSynthesisSettings(string VoiceId, int Volume = 80, int Rate = 0);

public interface IAudioCapture
{
    IReadOnlyList<AudioInputDevice> GetDevices();
    Task<PcmAudio> CaptureAsync(string deviceId, CaptureSettings settings, IProgress<AudioLevel>? progress,
        CancellationToken released, CancellationToken cancellationToken);
}
public interface ISpeechRecognitionProvider
{
    string Id { get; }
    string Name { get; }
    bool IsAvailable { get; }
    Task<SpeechRecognitionResult> RecognizeAsync(PcmAudio audio, CancellationToken cancellationToken);
}
public interface ITextToSpeechProvider
{
    IReadOnlyList<LocalVoice> GetVoices();
    Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken cancellationToken);
}
public sealed record SpeechWordBoundary(int Start, int Length, TimeSpan Position);
public sealed class TimedSpeech(PcmAudio audio, IReadOnlyList<SpeechWordBoundary> words, bool estimated) : IDisposable
{
    public PcmAudio Audio { get; } = audio;
    public IReadOnlyList<SpeechWordBoundary> Words { get; } = words;
    public bool Estimated { get; } = estimated;
    public void Dispose() => Audio.Dispose();
}
public interface ITimedTextToSpeechProvider : ITextToSpeechProvider
{
    Task<TimedSpeech> SynthesizeTimedAsync(string text, SpeechSynthesisSettings settings, CancellationToken token);
}
public interface IAudioPlayback
{
    Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? progress, CancellationToken cancellationToken);
}
