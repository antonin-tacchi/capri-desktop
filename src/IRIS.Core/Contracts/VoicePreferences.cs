using System;

namespace IRIS.Core.Contracts;

public sealed record VoicePreferences(string? MicrophoneId = null, string? RecognitionId = null, string? VoiceId = null,
    int GainPercent = 100, int ThresholdPermille = 5, int Volume = 80, int Rate = 0, bool PreferNatural = true)
{
    public void Validate()
    {
        static bool Valid(string? text, int maximum) => text == null || (text.Length <= maximum && !System.Linq.Enumerable.Any(text, char.IsControl));
        if (!Valid(MicrophoneId, 2048) || !Valid(RecognitionId, 80) || !Valid(VoiceId, 256)
            || GainPercent is < 50 or > 200 || ThresholdPermille is < 1 or > 200 || Volume is < 0 or > 100 || Rate is < -5 or > 5)
            throw new ArgumentException("Préférences vocales invalides.");
    }
}
