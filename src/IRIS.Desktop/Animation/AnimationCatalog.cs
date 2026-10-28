using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IRIS.Desktop.Animation;

internal sealed record AnimationDefinition(string Name, string Path, int Frames, double Fps, bool Loop,
    int FrameWidth, int FrameHeight, string Description, bool Experimental, bool Overlay, string? Error)
{
    public override string ToString() => Name + (Overlay ? " · calque seul" : Experimental ? " · prototype" : "") + (Error == null ? "" : " · indisponible");
}

internal sealed class AnimationCatalog
{
    public string Root { get; }
    public IReadOnlyList<AnimationDefinition> Animations { get; }
    public IReadOnlyList<string> Notices { get; }
    public IReadOnlyList<string> PngFiles { get; }
    public AnimationDefinition Get(string name) => Animations.SingleOrDefault(a => a.Name == name)
        ?? throw new InvalidDataException("Animation non déclarée : " + name);

    public AnimationCatalog(string directory)
    {
        Root = System.IO.Path.GetFullPath(directory);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("Dossier des animations absent : " + Root);
        if ((File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Le dossier des animations ne doit pas être un lien.");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        PngFiles = Directory.EnumerateFiles(Root, "*", options).Where(p => System.IO.Path.GetExtension(p).Equals(".png", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (PngFiles.Count > 512) throw new InvalidDataException("Trop de PNG dans le dossier des animations (maximum 512).");
        string manifest = System.IO.Path.Combine(Root, "animations.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("animations.json absent : les images et leur fréquence ne seront pas devinées.", manifest);
        if (new FileInfo(manifest).Length > 262144) throw new InvalidDataException("animations.json dépasse 256 Kio.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { MaxDepth = 12 });
        JsonElement root = document.RootElement;
        if (root.GetProperty("layout").GetString() != "horizontal_single_row") throw new InvalidDataException("Disposition de spritesheet non prise en charge.");
        JsonElement map = root.GetProperty("animations");
        if (map.ValueKind != JsonValueKind.Object || map.EnumerateObject().Count() is < 1 or > 128) throw new InvalidDataException("Catalogue d’animations invalide.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitions = new List<AnimationDefinition>();
        var notices = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in map.EnumerateObject())
        {
            string name = property.Name;
            if (!Regex.IsMatch(name, "^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant) || !names.Add(name)) throw new InvalidDataException("Nom d’animation invalide ou dupliqué : " + name);
            JsonElement item = property.Value;
            try
            {
                int count = item.GetProperty("frames").GetInt32();
                double fps = item.GetProperty("fps").GetDouble();
                bool loop = item.GetProperty("loop").GetBoolean();
                int width = item.TryGetProperty("frameWidth", out var w) ? w.GetInt32() : root.GetProperty("frameWidth").GetInt32();
                int height = item.TryGetProperty("frameHeight", out var h) ? h.GetInt32() : root.GetProperty("frameHeight").GetInt32();
                if (count is < 1 or > 120 || !double.IsFinite(fps) || fps is < 1 or > 60 || width is < 1 or > 2048 || height is < 1 or > 2048
                    || (long)width * height * count > 16777216) throw new InvalidDataException("Dimensions, nombre d’images ou FPS hors limites.");
                if (item.GetProperty("origin").GetString() != "bottom-center" || !item.GetProperty("alpha").GetBoolean()) throw new InvalidDataException("Origine bottom-center et transparence alpha requises.");
                string relative = item.GetProperty("file").GetString() ?? throw new InvalidDataException("Chemin vide.");
                if (System.IO.Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(p => p == "..") || relative.Contains(':')) throw new InvalidDataException("Chemin du PNG hors du dossier assets.");
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));
                if (!path.StartsWith(Root.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Chemin hors assets.");
                if (!PngFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    string[] candidates = PngFiles.Where(p => System.IO.Path.GetFileName(p).Equals(System.IO.Path.GetFileName(relative), StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (candidates.Length != 1) throw new FileNotFoundException(candidates.Length == 0 ? "PNG absent : " + relative : "Nom de PNG ambigu : " + relative);
                    path = candidates[0];
                    notices.Add(name + " : " + relative + " résolu vers " + System.IO.Path.GetRelativePath(Root, path));
                }
                ValidatePng(path, width * count, height);
                if ((item.TryGetProperty("sheetWidth", out var sw) && sw.GetInt32() != width * count)
                    || (item.TryGetProperty("sheetHeight", out var sh) && sh.GetInt32() != height)) throw new InvalidDataException("Dimensions du manifeste incohérentes.");
                used.Add(path);
                definitions.Add(new(name, path, count, fps, loop, width, height,
                    item.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "",
                    item.TryGetProperty("status", out var status) && status.GetString() == "prototype", name == "mouth", null));
            }
            catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {
                definitions.Add(new(name, "", 1, 1, false, 1, 1, "", false, name == "mouth", error.Message));
                notices.Add(name + " : " + error.Message);
            }
        }
        foreach (string file in PngFiles.Where(p => !used.Contains(p))) notices.Add("PNG non animé (absent du manifeste ou invalide) : " + System.IO.Path.GetRelativePath(Root, file));
        if (!definitions.Any(a => a.Name == "idle")) throw new InvalidDataException("Le manifeste doit déclarer une animation idle.");
        Animations = definitions.AsReadOnly(); Notices = notices.AsReadOnly();
    }

    internal static void ValidatePng(string path, int width, int height)
    {
        if (new FileInfo(path).Length > 67108864) throw new InvalidDataException("PNG trop volumineux (maximum 64 Mio).");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[33]; stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !header.Slice(12, 4).SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)) != width
            || BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4)) != height || header[24] != 8 || header[25] != 6)
            throw new InvalidDataException("PNG RGBA 8 bits attendu aux dimensions déclarées.");
    }
}
