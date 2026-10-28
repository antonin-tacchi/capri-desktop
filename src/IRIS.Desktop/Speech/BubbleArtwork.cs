using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;

namespace IRIS.Desktop.Speech;

internal sealed class BubbleArtwork : IDisposable
{
    private readonly Bitmap background;
    private readonly PrivateFontCollection fonts = new();
    private readonly Font font;
    private readonly Font caption;
    public Size Size => background.Size;
    public string FontName => font.FontFamily.Name;
    public RectangleF TextArea { get; }
    private readonly StringFormat format = new(StringFormat.GenericTypographic) { Trimming = StringTrimming.None, FormatFlags = StringFormatFlags.LineLimit };

    public BubbleArtwork(string assets, int width = 560)
    {
        if (width is < 300 or > 1000) throw new ArgumentOutOfRangeException(nameof(width));
        string png = Path.Combine(assets, "bulle_discution.png");
        string ttf = Path.Combine(assets, "fonts", "ShareTechMono-Regular.ttf");
        if (!File.Exists(png)) throw new FileNotFoundException("Bulle de discussion absente.", png);
        if (!File.Exists(ttf)) throw new FileNotFoundException("Police Share Tech Mono absente.", ttf);
        if (new FileInfo(png).Length > 8388608 || new FileInfo(ttf).Length > 1048576) throw new InvalidDataException("Ressource de bulle trop volumineuse.");
        using var source = new Bitmap(png);
        if (source.Width != 2172 || source.Height != 724 || !Image.IsAlphaPixelFormat(source.PixelFormat)) throw new InvalidDataException("Bulle RGBA 2172 × 724 attendue.");
        background = new Bitmap(width, width / 3, PixelFormat.Format32bppPArgb);
        try
        {
            using (Graphics draw = Graphics.FromImage(background))
            {
                draw.CompositingMode = CompositingMode.SourceCopy;
                draw.InterpolationMode = InterpolationMode.HighQualityBicubic;
                draw.DrawImage(source, new Rectangle(Point.Empty, background.Size));
            }
            fonts.AddFontFile(ttf);
            if (fonts.Families.Length != 1) throw new InvalidDataException("Police de la bulle invalide.");
            font = new Font(fonts.Families[0], Math.Max(14, width * 16f / 560), FontStyle.Regular, GraphicsUnit.Pixel);
            caption = new Font(fonts.Families[0], Math.Max(9, width * 11f / 560), FontStyle.Regular, GraphicsUnit.Pixel);
            TextArea = new(width * 0.105f, Size.Height * 0.335f, width * 0.77f, Size.Height * 0.325f);
        }
        catch { background.Dispose(); fonts.Dispose(); throw; }
    }
    public bool Fits(string text)
    {
        using Graphics measure = Graphics.FromImage(background);
        measure.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        measure.MeasureString(text, font, TextArea.Size, format, out int fitted, out _);
        return fitted >= text.Length;
    }
    public IReadOnlyList<string> Paginate(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 16000) throw new ArgumentException("Réponse de 1 à 16000 caractères attendue.");
        var pages = new List<string>();
        int offset = 0;
        while (offset < text.Length)
        {
            string rest = text[offset..];
            int[] elements = StringInfo.ParseCombiningCharacters(rest);
            int low = 1, high = Math.Min(elements.Length, 600), best = 0;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                int length = middle == elements.Length ? rest.Length : elements[middle];
                if (length <= 600 && Fits(rest[..length])) { best = length; low = middle + 1; }
                else high = middle - 1;
            }
            if (best == 0) throw new InvalidDataException("Zone de texte de la bulle trop petite.");
            if (best < rest.Length)
            {
                int sentence = 0;
                for (int i = best - 1; i >= best / 2; i--)
                    if ((rest[i] is '.' or '!' or '?' or '\n') && (i + 1 == rest.Length || char.IsWhiteSpace(rest[i + 1])))
                    {
                        sentence = i + 1;
                        while (sentence < best && char.IsWhiteSpace(rest[sentence])) sentence++;
                        break;
                    }
                // Prefer complete words; never split a grapheme if a long token must wrap.
                if (sentence > 0) best = sentence;
                else for (int i = best - 1; i >= best / 2; i--)
                        if (char.IsWhiteSpace(rest[i])) { best = i + 1; break; }
            }
            pages.Add(rest[..best]); offset += best;
        }
        return pages.AsReadOnly();
    }
    public Bitmap Render(string text, int page, int total)
    {
        if (!Fits(text)) throw new InvalidDataException("Le texte dépasse la zone lisible de la bulle.");
        var bitmap = new Bitmap(Size.Width, Size.Height, PixelFormat.Format32bppPArgb);
        using Graphics draw = Graphics.FromImage(bitmap);
        draw.DrawImageUnscaled(background, 0, 0);
        draw.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // No ClearType on a transparent surface.
        using var ink = new SolidBrush(Color.FromArgb(250, 240, 255));
        using var detail = new SolidBrush(Color.FromArgb(204, 170, 250));
        draw.DrawString(text, font, ink, TextArea, format);
        draw.DrawString($"{page + 1} / {total}    ·    Cliquer pour arrêter", caption, detail, TextArea.Left, Size.Height * 0.695f);
        return bitmap;
    }
    public void Dispose() { font.Dispose(); caption.Dispose(); fonts.Dispose(); format.Dispose(); background.Dispose(); }
}
