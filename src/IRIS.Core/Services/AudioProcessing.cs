using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using IRIS.Core.Contracts;

namespace IRIS.Core.Services;

public static class AudioProcessing
{
    public static AudioLevel Measure(ReadOnlySpan<byte> data, TimeSpan elapsed)
    {
        double squares = 0, peak = 0;
        for (int index = 0; index + 1 < data.Length; index += 2)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(data[index..]) / 32768.0;
            squares += sample * sample; peak = Math.Max(peak, Math.Abs(sample));
        }
        return new(data.Length == 0 ? 0 : Math.Sqrt(squares / (data.Length / 2)), peak, peak >= 0.999, elapsed);
    }
    public static void ApplyGain(Span<byte> data, double gain)
    {
        if (!double.IsFinite(gain) || gain is < 0.5 or > 2) throw new ArgumentOutOfRangeException(nameof(gain));
        for (int index = 0; index + 1 < data.Length; index += 2)
        {
            int sample = (int)Math.Round(BinaryPrimitives.ReadInt16LittleEndian(data[index..]) * gain);
            BinaryPrimitives.WriteInt16LittleEndian(data[index..], (short)Math.Clamp(sample, short.MinValue, short.MaxValue));
        }
    }
    public static PcmAudio TrimSilence(PcmAudio audio, double threshold)
    {
        if (!double.IsFinite(threshold) || threshold is < 0.0001 or > 0.2) throw new ArgumentOutOfRangeException(nameof(threshold));
        const int frame = 640, padding = 8000; // Keep 250 ms around soft word boundaries.
        ReadOnlySpan<byte> data = audio.Data.Span;
        int first = -1, last = 0;
        for (int offset = 0; offset < data.Length; offset += frame)
        {
            int length = Math.Min(frame, data.Length - offset);
            if (Measure(data.Slice(offset, length), TimeSpan.Zero).Rms < threshold) continue;
            if (first < 0) first = offset;
            last = offset + length;
        }
        if (first < 0) return new PcmAudio(ReadOnlySpan<byte>.Empty);
        int start = Math.Max(0, first - padding), end = Math.Min(data.Length, last + padding);
        return new PcmAudio(data[start..end]);
    }

    public static PcmAudio PrepareForRecognition(PcmAudio audio, double threshold, out SpeechSignalAnalysis analysis)
    {
        if (audio.SampleRateHz != 16000) throw new ArgumentException("La capture ASR doit être à 16 kHz.");
        if (!double.IsFinite(threshold) || threshold is < 0.001 or > 0.2) throw new ArgumentOutOfRangeException(nameof(threshold));
        byte[] centered = audio.Data.ToArray();
        byte[]? normalized = null;
        try
        {
            AudioLevel original = Measure(centered, audio.Duration);
            long sum = 0;
            for (int i = 0; i < centered.Length; i += 2) sum += BinaryPrimitives.ReadInt16LittleEndian(centered.AsSpan(i));
            double mean = centered.Length == 0 ? 0 : sum / (centered.Length / 2.0);
            for (int i = 0; i < centered.Length; i += 2)
                BinaryPrimitives.WriteInt16LittleEndian(centered.AsSpan(i), (short)Math.Clamp(Math.Round(BinaryPrimitives.ReadInt16LittleEndian(centered.AsSpan(i)) - mean), short.MinValue, short.MaxValue));
            double strongest = 0;
            for (int i = 0; i < centered.Length; i += 640) strongest = Math.Max(strongest, Measure(centered.AsSpan(i, Math.Min(640, centered.Length - i)), TimeSpan.Zero).Rms);
            bool tooQuiet = strongest < 0.00025;
            analysis = new(audio.Duration, original.Rms, original.Peak, original.Clipped, 1, tooQuiet);
            if (tooQuiet) return new PcmAudio(ReadOnlySpan<byte>.Empty);
            using var clean = new PcmAudio(centered);
            // A high UI threshold must not erase an entire softly spoken sentence.
            using PcmAudio trimmed = TrimSilence(clean, Math.Min(threshold, Math.Max(0.00025, strongest * 0.04)));
            AudioLevel measured = Measure(trimmed.Data.Span, trimmed.Duration);
            if (measured.Rms == 0 || measured.Peak == 0) return new PcmAudio(ReadOnlySpan<byte>.Empty);
            double gain = Math.Min(Math.Clamp(0.06 / measured.Rms, 1, 8), 0.95 / measured.Peak);
            normalized = trimmed.Data.ToArray();
            for (int i = 0; i < normalized.Length; i += 2)
                BinaryPrimitives.WriteInt16LittleEndian(normalized.AsSpan(i), (short)Math.Clamp(Math.Round(BinaryPrimitives.ReadInt16LittleEndian(normalized.AsSpan(i)) * gain), short.MinValue, short.MaxValue));
            analysis = analysis with { RecognitionGain = gain };
            return new PcmAudio(normalized);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(centered);
            if (normalized != null) CryptographicOperations.ZeroMemory(normalized);
        }
    }
}
