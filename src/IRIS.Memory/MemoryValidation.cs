using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IRIS.Core.Contracts;

namespace IRIS.Memory;

public sealed record MemoryExport(int Version, DateTimeOffset ExportedAt, IReadOnlyList<MemoryItem> Items);

public static class MemoryValidation
{
    public const int MaximumItems = 1000;
    public const int MaximumDocumentBytes = 8388608;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12,
        WriteIndented = true
    };
    private static readonly Regex Credentials = new(@"-----BEGIN [A-Z ]*PRIVATE KEY-----|\b(?:sk-[A-Za-z0-9_-]{16,}|nvapi-[A-Za-z0-9_-]{16,}|ghp_[A-Za-z0-9]{30,}|hf_[A-Za-z0-9]{20,}|Bearer\s+[A-Za-z0-9._-]{12,})|\b(?:mot\s+de\s+passe|password|api[_ -]?key|access[_ -]?token|secret|cookie)\s*(?::|=|\best\b|\bis\b)\s*\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static void Policy(MemoryPolicy policy)
    {
        if (policy.RetentionDays is < 1 or > 3650 || (!policy.Enabled && policy.ShareWithModel))
            throw new ArgumentException("Politique mémoire invalide.");
    }
    public static void Content(string title, string content, MemoryKind kind)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80 || string.IsNullOrWhiteSpace(content) || content.Length > 1000
            || title != title.Trim() || content != content.Trim() || !Enum.IsDefined(kind)
            || title.Any(char.IsControl) || content.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Titre (1–80 caractères), souvenir (1–1000) ou catégorie invalide.");
        if (Credentials.IsMatch(title + "\n" + content)) throw new ArgumentException("Ce texte ressemble à un identifiant secret. Ne stockez pas de mot de passe, clé ou jeton dans les souvenirs.");
    }
    public static string SearchKey(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.ToLowerInvariant(c));
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
    public static MemoryExport Parse(string document, DateTimeOffset now)
    {
        if (Encoding.UTF8.GetByteCount(document) > MaximumDocumentBytes) throw new InvalidDataException("Import limité à 8 Mio.");
        MemoryExport export = JsonSerializer.Deserialize<MemoryExport>(document, JsonOptions) ?? throw new InvalidDataException("Export vide.");
        if (export.Version != 1 || export.Items == null || export.Items.Count > MaximumItems
            || export.ExportedAt > now.AddMinutes(1) || export.ExportedAt < DateTimeOffset.UnixEpoch)
            throw new InvalidDataException("Version ou contenu de l’export incompatible.");
        var ids = new HashSet<Guid>();
        foreach (MemoryItem item in export.Items)
        {
            if (item == null) throw new InvalidDataException("Souvenir absent.");
            Content(item.Title, item.Content, item.Kind);
            if (item.Id == Guid.Empty || !ids.Add(item.Id) || item.Revision <= 0
                || item.CreatedAt < DateTimeOffset.UnixEpoch || item.CreatedAt > item.UpdatedAt || item.UpdatedAt > now
                || item.ExpiresAt <= now || item.ExpiresAt > item.UpdatedAt.AddDays(3650))
                throw new InvalidDataException("Souvenir importé invalide, dupliqué ou expiré.");
        }
        return export;
    }
}
