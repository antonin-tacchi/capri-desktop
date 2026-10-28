using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace IRIS.Desktop.Animation;

internal sealed class SpriteFrames : IDisposable
{
    public AnimationDefinition Definition { get; }
    public Bitmap[] Frames { get; }
    public long RetainedBytes => (long)Frames.Length * Frames[0].Width * Frames[0].Height * 4;
    private SpriteFrames(AnimationDefinition definition, Bitmap[] frames) { Definition = definition; Frames = frames; }

    public static SpriteFrames Load(AnimationDefinition definition, int displayHeight, CancellationToken token)
    {
        if (definition.Error != null) throw new InvalidDataException(definition.Name + " : " + definition.Error);
        if (displayHeight is < 64 or > 512) throw new ArgumentOutOfRangeException(nameof(displayHeight));
        AnimationCatalog.ValidatePng(definition.Path, definition.FrameWidth * definition.Frames, definition.FrameHeight);
        token.ThrowIfCancellationRequested();
        using var stream = new FileStream(definition.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sheet = new Bitmap(stream);
        if (sheet.Width != definition.FrameWidth * definition.Frames || sheet.Height != definition.FrameHeight) throw new InvalidDataException("PNG décodé incompatible avec le manifeste.");
        int width = Math.Max(1, (int)Math.Round(displayHeight * (double)definition.FrameWidth / definition.FrameHeight));
        if ((long)width * displayHeight * definition.Frames * 4 > 33554432) throw new InvalidDataException("Animation réduite supérieure à 32 Mio.");
        var frames = new Bitmap[definition.Frames];
        try
        {
            for (int index = 0; index < frames.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var bitmap = new Bitmap(width, displayHeight, PixelFormat.Format32bppPArgb); frames[index] = bitmap;
                using Graphics graphics = Graphics.FromImage(bitmap);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(sheet, new Rectangle(0, 0, width, displayHeight), new Rectangle(index * definition.FrameWidth, 0, definition.FrameWidth, definition.FrameHeight), GraphicsUnit.Pixel);
            }
            token.ThrowIfCancellationRequested();
            return new(definition, frames);
        }
        catch { foreach (Bitmap? frame in frames) frame?.Dispose(); throw; }
    }
    public void Dispose() { foreach (Bitmap frame in Frames) frame.Dispose(); }
}

// Same elapsed-time principle as Capri's Clip/Advance; counts, FPS and loop now
// come from the manifest. Delayed UI ticks skip frames instead of slowing playback.
internal sealed class AnimationTimeline(int frames, double fps, bool loop)
{
    public int FrameAt(double elapsed) => loop ? (int)(Math.Floor(Math.Max(0, elapsed) * fps) % frames)
        : (int)Math.Min(frames - 1, Math.Floor(Math.Max(0, elapsed) * fps));
    public bool CompleteAt(double elapsed) => !loop && elapsed >= frames / fps;
}
