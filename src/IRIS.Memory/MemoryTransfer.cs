using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.Memory;

public static class MemoryTransfer
{
    private static string LocalPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(full)!).DriveType is not (DriveType.Fixed or DriveType.Removable))
            throw new IOException("Choisissez un fichier sur un disque local ou amovible.");
        return full;
    }
    public static async Task<string> ReadAsync(string path, CancellationToken token)
    {
        using var stream = new FileStream(LocalPath(path), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > MemoryValidation.MaximumDocumentBytes) throw new InvalidDataException("Import limité à 8 Mio.");
        byte[] bytes = new byte[MemoryValidation.MaximumDocumentBytes + 1];
        int length = 0, count;
        while ((count = await stream.ReadAsync(bytes.AsMemory(length), token).ConfigureAwait(false)) > 0)
        {
            length += count;
            if (length > MemoryValidation.MaximumDocumentBytes) throw new InvalidDataException("Import limité à 8 Mio.");
        }
        return new UTF8Encoding(false, true).GetString(bytes, 0, length);
    }
    public static async Task WriteNewAsync(string path, string document, CancellationToken token)
    {
        string destination = LocalPath(path);
        byte[] data = new UTF8Encoding(false, true).GetBytes(document);
        if (data.Length > MemoryValidation.MaximumDocumentBytes) throw new InvalidDataException("Export trop volumineux.");
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".iris-memory-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool created = false;
        try
        {
            token.ThrowIfCancellationRequested();
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                created = true;
                await stream.WriteAsync(data, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
                stream.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, false);
            created = false;
        }
        finally { if (created && File.Exists(temporary)) File.Delete(temporary); }
    }
}
