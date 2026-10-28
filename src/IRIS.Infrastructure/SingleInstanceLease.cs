using System;
using System.IO;

namespace IRIS.Infrastructure;

public sealed class SingleInstanceLease : IDisposable
{
    private readonly FileStream stream;
    private SingleInstanceLease(FileStream stream) => this.stream = stream;

    public static SingleInstanceLease? TryAcquire(string directory)
    {
        Directory.CreateDirectory(directory);
        try { return new(new FileStream(Path.Combine(directory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 32 or 33) { return null; }
    }
    // Leave the empty lock file in place: removing it after release introduces a race.
    public void Dispose() => stream.Dispose();
}
