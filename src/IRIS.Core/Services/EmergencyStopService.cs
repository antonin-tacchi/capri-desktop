using System;
using System.Threading;

namespace IRIS.Core.Services;

public sealed class EmergencyStopService : IDisposable
{
    private readonly CancellationTokenSource source = new();
    public CancellationToken Token => source.Token;
    public bool IsStopped => source.IsCancellationRequested;
    public void Stop() => source.Cancel();
    public void Dispose() => source.Dispose();
}
