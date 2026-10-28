using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Memory.Storage;

namespace IRIS.Memory;

[SupportedOSPlatform("windows")]
public sealed class MemoryService : IMemoryService
{
    private readonly string path;
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly TimeProvider time;
    public static string SqliteVersion => SqliteDatabase.Version;
    private const string Columns = "id,title,content,kind,created,updated,expires,revision";

    public MemoryService(string directory, TimeProvider? time = null)
    {
        path = Path.Combine(Path.GetFullPath(directory), "memory.db");
        this.time = time ?? TimeProvider.System;
    }
    private async Task<T> RunAsync<T>(Func<SqliteDatabase, DateTimeOffset, CancellationToken, T> action, CancellationToken token)
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var database = new SqliteDatabase(path);
                using var registration = token.Register(database.Interrupt);
                return database.Transaction(() =>
                {
                    token.ThrowIfCancellationRequested();
                    DateTimeOffset now = time.GetUtcNow();
                    if (database.Execute("DELETE FROM memories WHERE expires<=?1", now.ToUnixTimeMilliseconds()) > 0) Bump(database);
                    T result = action(database, now, token);
                    token.ThrowIfCancellationRequested();
                    return result;
                });
            }, token).ConfigureAwait(false);
        }
        catch (MemoryStorageException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        finally { serial.Release(); }
    }
    private static long Bump(SqliteDatabase db)
    {
        db.Execute("UPDATE memory_state SET revision=revision+1 WHERE id=1");
        return db.Scalar("SELECT revision FROM memory_state WHERE id=1");
    }
    private static MemoryPolicy ReadPolicy(SqliteDatabase db)
    {
        using var row = db.Query("SELECT enabled,share,retention FROM memory_policy WHERE id=1");
        if (!row.Read()) throw new InvalidDataException("Politique mémoire absente.");
        if (row.Number(0) is not (0 or 1) || row.Number(1) is not (0 or 1)) throw new InvalidDataException("Politique mémoire invalide.");
        var policy = new MemoryPolicy(row.Number(0) == 1, row.Number(1) == 1, checked((int)row.Number(2)));
        MemoryValidation.Policy(policy);
        return policy;
    }
    private static MemoryPolicy RequireEnabled(SqliteDatabase db)
    {
        MemoryPolicy policy = ReadPolicy(db);
        if (!policy.Enabled) throw new InvalidOperationException("La mémorisation est désactivée.");
        return policy;
    }
    private static MemoryItem ReadItem(SqliteStatement row)
    {
        var item = new MemoryItem(Guid.Parse(row.Text(0)), row.Text(1), row.Text(2), (MemoryKind)checked((int)row.Number(3)),
            DateTimeOffset.FromUnixTimeMilliseconds(row.Number(4)), DateTimeOffset.FromUnixTimeMilliseconds(row.Number(5)),
            DateTimeOffset.FromUnixTimeMilliseconds(row.Number(6)), row.Number(7));
        MemoryValidation.Content(item.Title, item.Content, item.Kind);
        if (item.Id == Guid.Empty || item.Revision <= 0 || item.CreatedAt > item.UpdatedAt || item.ExpiresAt <= item.UpdatedAt)
            throw new InvalidDataException("Souvenir stocké invalide.");
        return item;
    }
    private static List<MemoryItem> ReadItems(SqliteDatabase db, string search, CancellationToken token)
    {
        var items = new List<MemoryItem>();
        using var rows = db.Query("SELECT " + Columns + " FROM memories WHERE instr(search_text,?1)>0 ORDER BY updated DESC,id LIMIT 1001", MemoryValidation.SearchKey(search));
        while (rows.Read()) { token.ThrowIfCancellationRequested(); items.Add(ReadItem(rows)); }
        if (items.Count > MemoryValidation.MaximumItems) throw new InvalidDataException("Capacité mémoire dépassée.");
        return items;
    }
    private static DateTimeOffset Expiration(DateTimeOffset updated, MemoryKind kind, MemoryPolicy policy) => updated.AddDays(kind == MemoryKind.Contextual ? Math.Min(7, policy.RetentionDays) : policy.RetentionDays);
    private static void Insert(SqliteDatabase db, MemoryItem item)
    {
        db.Execute("INSERT INTO memories (id,title,content,kind,created,updated,expires,revision,search_text) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9)",
            item.Id.ToString("D"), item.Title, item.Content, (int)item.Kind, item.CreatedAt.ToUnixTimeMilliseconds(), item.UpdatedAt.ToUnixTimeMilliseconds(),
            item.ExpiresAt.ToUnixTimeMilliseconds(), item.Revision, MemoryValidation.SearchKey(item.Title + " " + item.Content));
    }
    public Task<MemoryPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => RunAsync((db, _, _) => ReadPolicy(db), cancellationToken);
    public Task SetPolicyAsync(MemoryPolicy policy, CancellationToken cancellationToken = default)
    {
        MemoryValidation.Policy(policy);
        return RunAsync((db, _, _) =>
        {
            if (ReadPolicy(db) == policy) return 0;
            db.Execute("UPDATE memory_policy SET enabled=?1,share=?2,retention=?3 WHERE id=1", policy.Enabled ? 1 : 0, policy.ShareWithModel ? 1 : 0, policy.RetentionDays);
            db.Execute("UPDATE memories SET expires=MIN(expires,updated+(CASE WHEN kind=2 THEN MIN(7,?1) ELSE ?1 END)*86400000)", policy.RetentionDays);
            Bump(db);
            return 0;
        }, cancellationToken);
    }
    public Task<IReadOnlyList<MemoryItem>> ListAsync(string search = "", CancellationToken cancellationToken = default)
    {
        if (search.Length > 200 || search.Contains('\0')) throw new ArgumentException("Recherche limitée à 200 caractères.");
        return RunAsync<IReadOnlyList<MemoryItem>>((db, _, token) => ReadItems(db, search, token), cancellationToken);
    }
    public Task<MemoryItem> AddAsync(string title, string content, MemoryKind kind, CancellationToken cancellationToken = default)
    {
        MemoryValidation.Content(title, content, kind);
        return RunAsync((db, now, _) =>
        {
            MemoryPolicy policy = RequireEnabled(db);
            if (db.Scalar("SELECT COUNT(*) FROM memories") >= MemoryValidation.MaximumItems) throw new InvalidOperationException("La mémoire contient déjà 1000 souvenirs.");
            var item = new MemoryItem(Guid.NewGuid(), title, content, kind, now, now, Expiration(now, kind, policy), Bump(db));
            Insert(db, item);
            return item;
        }, cancellationToken);
    }
    public Task UpdateAsync(Guid id, long revision, string title, string content, MemoryKind kind, CancellationToken cancellationToken = default)
    {
        MemoryValidation.Content(title, content, kind);
        if (id == Guid.Empty || revision <= 0) throw new ArgumentException("Identité du souvenir invalide.");
        return RunAsync((db, now, _) =>
        {
            MemoryPolicy policy = RequireEnabled(db);
            int changed = db.Execute("UPDATE memories SET title=?1,content=?2,kind=?3,updated=?4,expires=?5,revision=?6,search_text=?7 WHERE id=?8 AND revision=?9",
                title, content, (int)kind, now.ToUnixTimeMilliseconds(), Expiration(now, kind, policy).ToUnixTimeMilliseconds(), Bump(db),
                MemoryValidation.SearchKey(title + " " + content), id.ToString("D"), revision);
            if (changed != 1) throw new InvalidOperationException("Le souvenir a changé ou a expiré. Rechargez la liste.");
            return 0;
        }, cancellationToken);
    }
    public Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || revision <= 0) throw new ArgumentException("Identité du souvenir invalide.");
        return RunAsync((db, _, _) =>
        {
            if (db.Execute("DELETE FROM memories WHERE id=?1 AND revision=?2", id.ToString("D"), revision) != 1)
                throw new InvalidOperationException("Le souvenir a changé ou a expiré. Rechargez la liste.");
            Bump(db);
            return 0;
        }, cancellationToken);
    }
    public Task ClearAsync(CancellationToken cancellationToken = default) => RunAsync((db, _, _) => { if (db.Execute("DELETE FROM memories") > 0) Bump(db); return 0; }, cancellationToken);
    public Task<string> ExportAsync(CancellationToken cancellationToken = default) => RunAsync((db, now, token) =>
        JsonSerializer.Serialize(new MemoryExport(1, now, ReadItems(db, "", token)), MemoryValidation.JsonOptions), cancellationToken);

    public Task<int> PreviewImportAsync(string document, CancellationToken cancellationToken = default) => RunAsync((db, now, _) =>
    {
        MemoryExport export = MemoryValidation.Parse(document, now);
        ValidateImport(db, export, ReadPolicy(db), now);
        return export.Items.Count;
    }, cancellationToken);
    private static void ValidateImport(SqliteDatabase db, MemoryExport export, MemoryPolicy policy, DateTimeOffset now)
    {
        if (db.Scalar("SELECT COUNT(*) FROM memories") + export.Items.Count > MemoryValidation.MaximumItems) throw new InvalidDataException("L’import dépasserait 1000 souvenirs.");
        foreach (MemoryItem item in export.Items)
        {
            if (db.Scalar("SELECT COUNT(*) FROM memories WHERE id=?1", item.Id.ToString("D")) != 0) throw new InvalidDataException("L’import contient un souvenir déjà présent ; aucun écrasement.");
            if (Expiration(item.UpdatedAt, item.Kind, policy) <= now) throw new InvalidDataException("Un souvenir importé dépasse la durée de conservation actuelle.");
        }
    }
    public Task<int> ImportAsync(string document, CancellationToken cancellationToken = default) => RunAsync((db, now, token) =>
    {
        MemoryPolicy policy = RequireEnabled(db);
        MemoryExport export = MemoryValidation.Parse(document, now);
        ValidateImport(db, export, policy, now);
        long revision = Bump(db);
        foreach (MemoryItem item in export.Items)
        {
            token.ThrowIfCancellationRequested();
            DateTimeOffset limit = Expiration(item.UpdatedAt, item.Kind, policy);
            Insert(db, item with { Revision = revision, ExpiresAt = item.ExpiresAt < limit ? item.ExpiresAt : limit });
        }
        return export.Items.Count;
    }, cancellationToken);

    public Task<MemoryContext> RetrieveAsync(string question, CancellationToken cancellationToken)
    {
        if (question.Length > 4096) throw new ArgumentException("Question trop longue.");
        return RunAsync((db, _, token) =>
        {
            MemoryPolicy policy = ReadPolicy(db);
            long revision = db.Scalar("SELECT revision FROM memory_state WHERE id=1");
            if (!policy.Enabled || !policy.ShareWithModel) return new MemoryContext(revision, Array.Empty<MemorySnippet>());
            var ignored = new HashSet<string>(new[] { "les", "des", "une", "est", "dans", "pour", "avec", "que", "qui", "mon", "mes", "moi", "sur", "quoi", "comment", "quel", "quelle", "sont", "peux", "bonjour", "iris" }, StringComparer.Ordinal);
            string[] words = Regex.Matches(MemoryValidation.SearchKey(question), @"[\p{L}\p{N}]{3,}").Select(match => match.Value).Where(word => !ignored.Contains(word)).Distinct().Take(6).ToArray();
            if (words.Length == 0) return new MemoryContext(revision, Array.Empty<MemorySnippet>());
            var selected = new List<MemorySnippet>();
            int length = 0;
            foreach (MemoryItem item in ReadItems(db, "", token))
            {
                string key = MemoryValidation.SearchKey(item.Title + " " + item.Content);
                if (!words.Any(word => key.Contains(word, StringComparison.Ordinal)) || length + item.Content.Length > 3000) continue;
                selected.Add(new(item.Title, item.Content, item.Kind)); length += item.Content.Length;
                if (selected.Count == 5) break;
            }
            return new MemoryContext(revision, selected.AsReadOnly());
        }, cancellationToken);
    }
}
