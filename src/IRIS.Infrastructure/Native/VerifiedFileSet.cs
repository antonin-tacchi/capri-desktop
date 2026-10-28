using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.Infrastructure.Native;

internal sealed record TrustedModelPackage(string ArchiveName, string ArchiveSha, string RuntimeName, string ModelName, string ModelSha, long ModelBytes);

internal sealed class VerifiedFileSet : IDisposable
{
    private readonly List<FileStream> leases = new();
    private FileStream Open(string path)
    {
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        leases.Add(file); return file;
    }
    private static async Task<string> HashAsync(Stream stream, CancellationToken token) => Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));

    public static async Task<VerifiedFileSet> LoadAsync(string root, TrustedModelPackage package, CancellationToken token)
    {
        var verified = new VerifiedFileSet();
        try
        {
            string runtime = Path.GetFullPath(Path.Combine(root, "runtimes", package.RuntimeName));
            string prefix = runtime.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Directory.Exists(runtime)) throw new DirectoryNotFoundException("Dossier du moteur absent : " + runtime);
            if ((File.GetAttributes(runtime) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Le dossier du moteur est un lien non autorisé.");
            FileStream archive = verified.Open(Path.Combine(root, "packages", package.ArchiveName));
            if (await HashAsync(archive, token).ConfigureAwait(false) != package.ArchiveSha) throw new InvalidDataException("Archive du moteur altérée.");
            archive.Position = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string full = Path.GetFullPath(Path.Combine(runtime, entry.FullName));
                    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains(':')) throw new InvalidDataException("Entrée de runtime hors périmètre.");
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                    if (!names.Add(full)) throw new InvalidDataException("Entrée de runtime dupliquée.");
                    FileStream installed = verified.Open(full);
                    using Stream expected = entry.Open();
                    if (installed.Length != entry.Length || await HashAsync(installed, token).ConfigureAwait(false) != await HashAsync(expected, token).ConfigureAwait(false))
                        throw new InvalidDataException("Fichier du moteur différent de l’archive vérifiée.");
                }
            }
            var directories = new Stack<string>(); directories.Push(runtime);
            int count = 0;
            while (directories.Count > 0)
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(directories.Pop()))
                {
                    token.ThrowIfCancellationRequested();
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Lien supplémentaire dans le runtime.");
                    if ((attributes & FileAttributes.Directory) != 0) directories.Push(path);
                    else { count++; if (!names.Contains(Path.GetFullPath(path))) throw new InvalidDataException("Fichier supplémentaire dans le runtime."); }
                }
            }
            if (count != names.Count) throw new InvalidDataException("Runtime incomplet.");
            FileStream model = verified.Open(Path.Combine(root, "models", package.ModelName));
            if (model.Length != package.ModelBytes || await HashAsync(model, token).ConfigureAwait(false) != package.ModelSha) throw new InvalidDataException("Modèle absent, incomplet ou altéré.");
            return verified;
        }
        catch { verified.Dispose(); throw; }
    }
    public void Dispose() { foreach (FileStream file in leases) file.Dispose(); leases.Clear(); }
}
