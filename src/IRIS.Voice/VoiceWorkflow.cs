using System;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;

namespace IRIS.Voice;

public sealed class VoiceWorkflow(IAudioCapture capture, ITextToSpeechProvider synthesis, IAudioPlayback playback)
{
    public async Task<SpeechRecognitionResult> CaptureAndRecognizeAsync(bool consent, string deviceId, CaptureSettings settings,
        ISpeechRecognitionProvider recognition, IProgress<AudioLevel>? level, Action onMicrophoneClosed, CancellationToken released, CancellationToken token,
        Action<SpeechSignalAnalysis>? reportAnalysis = null)
    {
        if (!consent) throw new InvalidOperationException("Autorisez explicitement le microphone pour cette fenêtre.");
        if (!recognition.IsAvailable) throw new InvalidOperationException("Le moteur de reconnaissance sélectionné est indisponible.");
        if (string.IsNullOrWhiteSpace(deviceId)) throw new InvalidOperationException("Sélectionnez un microphone.");
        using PcmAudio audio = await capture.CaptureAsync(deviceId, settings, level, released, token).ConfigureAwait(false);
        onMicrophoneClosed();
        token.ThrowIfCancellationRequested();
        using PcmAudio prepared = AudioProcessing.PrepareForRecognition(audio, settings.SpeechThreshold, out SpeechSignalAnalysis analysis);
        reportAnalysis?.Invoke(analysis);
        token.ThrowIfCancellationRequested();
        if (prepared.Data.Length == 0) return new("", null, recognition.Id);
        return await recognition.RecognizeAsync(prepared, token).ConfigureAwait(false);
    }
    public async Task SpeakAsync(string text, SpeechSynthesisSettings settings, IProgress<AudioLevel>? level, CancellationToken token)
    {
        using PcmAudio audio = await synthesis.SynthesizeAsync(text, settings, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await playback.PlayAsync(audio, level, token).ConfigureAwait(false);
    }
}
