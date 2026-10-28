using System;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;

namespace IRIS.Desktop;

internal sealed class ObservedAudioPlayback(IAudioPlayback playback, Action<bool> reportSpeaking) : IAudioPlayback
{
    public async Task PlayAsync(PcmAudio audio, IProgress<AudioLevel>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        reportSpeaking(true);
        try { await playback.PlayAsync(audio, progress, token).ConfigureAwait(false); }
        finally { reportSpeaking(false); }
    }
}
