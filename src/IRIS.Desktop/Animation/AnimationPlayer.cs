using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IRIS.Desktop.Animation;

internal sealed class AnimationPlayer : IDisposable
{
    private readonly AnimationCatalog catalog;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    private readonly SemaphoreSlim decoder = new(1);
    private readonly Stopwatch clock = new();
    private CancellationTokenSource? loading;
    private SpriteFrames? frames;
    private AnimationTimeline? timeline;
    private bool disposed;
    private int generation;
    internal int RequestVersion => generation;
    public int Height { get; set; }
    public int FrameIndex { get; private set; }
    public Bitmap? CurrentFrame => frames?.Frames[FrameIndex];
    public AnimationDefinition? Current => frames?.Definition;
    public bool Playing => timer.Enabled;
    public double Elapsed => clock.Elapsed.TotalSeconds;
    public long RetainedBytes => frames?.RetainedBytes ?? 0;
    public event Action? FrameChanged;
    public event Action? Completed;
    public event Action<string>? Error;
    public AnimationPlayer(AnimationCatalog catalog, int height)
    {
        this.catalog = catalog; Height = height;
        timer.Tick += (_, _) =>
        {
            if (timeline == null) return;
            int next = timeline.FrameAt(Elapsed);
            if (next != FrameIndex) { FrameIndex = next; FrameChanged?.Invoke(); }
            if (timeline.CompleteAt(Elapsed)) { timer.Stop(); clock.Stop(); Completed?.Invoke(); }
        };
    }
    public async Task<bool> PlayAsync(string name)
    {
        if (disposed) return false;
        Stop();
        int ticket = ++generation;
        using var request = new CancellationTokenSource(); loading = request;
        SpriteFrames? next = null;
        try
        {
            AnimationDefinition definition = catalog.Get(name);
            int height = Height;
            next = await Task.Run(async () =>
            {
                await decoder.WaitAsync(request.Token).ConfigureAwait(false);
                try { return SpriteFrames.Load(definition, height, request.Token); }
                finally { decoder.Release(); }
            }, request.Token);
            if (disposed || request.IsCancellationRequested || ticket != generation) return false;
            SpriteFrames? previous = frames; frames = next; next = null; previous?.Dispose();
            FrameIndex = 0; timeline = new(definition.Frames, definition.Fps, definition.Loop);
            clock.Restart(); timer.Start(); FrameChanged?.Invoke(); return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception error)
        {
            if (!disposed && ticket == generation) Error?.Invoke("Animation « " + name + " » impossible : " + error.Message);
            return false;
        }
        finally { next?.Dispose(); if (ReferenceEquals(loading, request)) loading = null; }
    }
    public void Stop() { generation++; loading?.Cancel(); timer.Stop(); clock.Stop(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Stop(); timer.Dispose(); frames?.Dispose(); frames = null;
        // The semaphore stays alive until any cancelled decoder has returned.
    }
}
