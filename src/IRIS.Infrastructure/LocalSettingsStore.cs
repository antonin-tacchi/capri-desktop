using System;
using System.IO;
using System.Text.Json;
using IRIS.Core.Contracts;

namespace IRIS.Infrastructure;

public sealed record IrisSettings
{
    public int Version { get; init; } = 1;
    public bool AlwaysOnTop { get; init; }
    public int CharacterScale { get; init; } = 100;
    public bool CharacterVisible { get; init; } = true;
    public int? CharacterAnchorX { get; init; }
    public int? CharacterAnchorY { get; init; }
    public int EmergencyFunctionKey { get; init; } = 12;
    public int Humor { get; init; } = 75;
    public VoicePreferences Voice { get; init; } = new();
    public bool SpeakReplies { get; init; } = true;

    public void Validate()
    {
        if (Version != 1 || CharacterScale < 50 || CharacterScale > 200
            || EmergencyFunctionKey < 6 || EmergencyFunctionKey > 12 || Humor is < 0 or > 100)
            throw new InvalidDataException("Paramètres invalides ou version incompatible.");
        if (Voice == null) throw new InvalidDataException("Préférences vocales absentes.");
        Voice.Validate();
        if (CharacterAnchorX.HasValue != CharacterAnchorY.HasValue || CharacterAnchorX is < -100000 or > 100000 || CharacterAnchorY is < -100000 or > 100000)
            throw new InvalidDataException("Position du personnage invalide.");
    }
}

public sealed class LocalSettingsStore
{
    private readonly string path;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IRIS");
    public LocalSettingsStore(string directory) => path = Path.Combine(Path.GetFullPath(directory), "settings.json");

    public IrisSettings Load()
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Fichier de paramètres trop volumineux.");
        IrisSettings settings = JsonSerializer.Deserialize<IrisSettings>(File.ReadAllText(path),
            new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Paramètres vides.");
        settings.Validate();
        return settings;
    }

    public void Save(IrisSettings settings)
    {
        settings.Validate();
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
