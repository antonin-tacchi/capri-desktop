using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IRIS.Core.Contracts;

public sealed class PcmAudio : IDisposable
{
    public const int SampleRate = 16000;
    public const int MaximumCaptureBytes = SampleRate * 2 * 30;
    public const int MaximumBytes = SampleRate * 2 * 120;
    private byte[] data;
    private bool disposed;
    public int SampleRateHz { get; }
    public ReadOnlyMemory<byte> Data { get { ObjectDisposedException.ThrowIf(disposed, this); return data; } }
    public TimeSpan Duration => TimeSpan.FromSeconds(Data.Length / (double)(SampleRateHz * 2));
    public Stream OpenRead() { ObjectDisposedException.ThrowIf(disposed, this); return new MemoryStream(data, 0, data.Length, false, false); }
    public PcmAudio(ReadOnlySpan<byte> pcm, int sampleRateHz = SampleRate)
    {
        if (sampleRateHz is not (16000 or 22050) || pcm.Length > sampleRateHz * 2 * 120 || pcm.Length % 2 != 0) throw new ArgumentException("Audio PCM mono 16 ou 22,05 kHz / 16 bits, deux minutes maximum.");
        SampleRateHz = sampleRateHz;
        data = pcm.ToArray();
    }
    public byte[] ToWave()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + data.Length); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRateHz); writer.Write(SampleRateHz * 2);
        writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(data.Length); writer.Write(data);
        return stream.ToArray();
    }
    public static PcmAudio FromWave(ReadOnlySpan<byte> wave)
    {
        if (wave.Length < 44 || wave.Length > MaximumBytes + 65536 || !wave[..4].SequenceEqual("RIFF"u8) || !wave.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Fichier WAV invalide ou trop long.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(wave.Slice(4, 4)) != wave.Length - 8) throw new InvalidDataException("Taille WAV incohérente.");
        bool format = false;
        int offset = 12;
        while (offset <= wave.Length - 8)
        {
            ReadOnlySpan<byte> name = wave.Slice(offset, 4);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(wave.Slice(offset + 4, 4));
            offset += 8;
            if (length > wave.Length - offset) throw new InvalidDataException("WAV tronqué.");
            var chunk = wave.Slice(offset, (int)length);
            if (name.SequenceEqual("fmt "u8))
                format = length >= 16 && BinaryPrimitives.ReadUInt16LittleEndian(chunk) == 1 && BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]) == 1
                    && BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]) == SampleRate && BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]) == 2
                    && BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]) == 16;
            if (name.SequenceEqual("data"u8))
            {
                if (!format) throw new InvalidDataException("WAV attendu : PCM mono 16 kHz / 16 bits.");
                return new PcmAudio(chunk);
            }
            offset += checked((int)length + (int)(length % 2));
        }
        throw new InvalidDataException("Données audio WAV absentes.");
    }
    public void Dispose() { if (!disposed) { CryptographicOperations.ZeroMemory(data); data = Array.Empty<byte>(); disposed = true; } }
}
