using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.Core.Contracts;

public enum MemoryKind { LongTerm, Preference, Contextual }
public sealed record MemoryPolicy(bool Enabled = false, bool ShareWithModel = false, int RetentionDays = 90);
public sealed record MemoryItem(Guid Id, string Title, string Content, MemoryKind Kind,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt, long Revision);
public sealed record MemorySnippet(string Title, string Content, MemoryKind Kind);
public sealed record MemoryContext(long Revision, IReadOnlyList<MemorySnippet> Items);

public interface IMemoryContextProvider
{
    Task<MemoryContext> RetrieveAsync(string question, CancellationToken cancellationToken);
}

public interface IMemoryService : IMemoryContextProvider
{
    Task<MemoryPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task SetPolicyAsync(MemoryPolicy policy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> ListAsync(string search = "", CancellationToken cancellationToken = default);
    Task<MemoryItem> AddAsync(string title, string content, MemoryKind kind, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, long revision, string title, string content, MemoryKind kind, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    Task<string> ExportAsync(CancellationToken cancellationToken = default);
    Task<int> PreviewImportAsync(string document, CancellationToken cancellationToken = default);
    Task<int> ImportAsync(string document, CancellationToken cancellationToken = default);
}
