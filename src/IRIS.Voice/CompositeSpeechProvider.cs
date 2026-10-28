using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;

namespace IRIS.Voice;

public sealed class CompositeSpeechProvider(params ITextToSpeechProvider[] providers) : ITimedTextToSpeechProvider
{
    public IReadOnlyList<LocalVoice> GetVoices() => providers.SelectMany(p => p.GetVoices()).ToArray();
    private ITextToSpeechProvider Select(string id) => providers.FirstOrDefault(p => p.GetVoices().Any(v => v.Id == id)) ?? throw new InvalidOperationException("Voix locale indisponible : " + id);
    public Task<PcmAudio> SynthesizeAsync(string text, SpeechSynthesisSettings settings, CancellationToken token) => Select(settings.VoiceId).SynthesizeAsync(text, settings, token);
    public async Task<TimedSpeech> SynthesizeTimedAsync(string text, SpeechSynthesisSettings settings, CancellationToken token)
    {
        ITextToSpeechProvider selected = Select(settings.VoiceId);
        if (selected is ITimedTextToSpeechProvider timed) return await timed.SynthesizeTimedAsync(text, settings, token).ConfigureAwait(false);
        PcmAudio audio = await selected.SynthesizeAsync(text, settings, token).ConfigureAwait(false);
        return new(audio, SpeechTiming.Estimate(text, audio.Duration), true);
    }
}
