using System.Collections.Generic;
using IRIS.Core.Contracts;

namespace IRIS.Desktop;

internal sealed record VoiceServices(IAudioCapture Capture, IReadOnlyList<ISpeechRecognitionProvider> Recognizers,
    ITextToSpeechProvider Synthesis, IAudioPlayback Playback);
