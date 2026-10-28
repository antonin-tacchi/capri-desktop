using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IRIS.Desktop.Animation;

internal sealed class CharacterWindow : Form
{
    private readonly AnimationCatalog catalog;
    private readonly Action<string> report;
    private readonly Action<Point> savePosition;
    private readonly Action openPreview;
    private readonly System.Windows.Forms.Timer behavior = new() { Interval = 50 };
    private readonly Stopwatch time = Stopwatch.StartNew();
    private readonly Random random = new();
    private double lastActivity, nextBlink;
    private bool stopped, manual, thinking, speaking, dragging, walking, leaving, wanted, behaviorPending;
    private Point dragOffset, anchor, walkStart, walkEnd;
    private double walkStarted;
    private readonly bool nativeRendering;
    internal AnimationPlayer Player { get; }
    internal Point BottomAnchor => anchor;
    internal bool IsStopped => stopped;

    public CharacterWindow(AnimationCatalog catalog, int scale, bool onTop, Point? savedPosition, Action<Point> savePosition,
        Action<string> report, Action openPreview, bool nativeRendering = true)
    {
        this.catalog = catalog; this.savePosition = savePosition; this.report = report; this.openPreview = openPreview; this.nativeRendering = nativeRendering;
        Text = "IRIS — Personnage"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None; TopMost = onTop;
        Player = new(catalog, Math.Clamp(256 * scale / 100, 128, 512));
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        anchor = savedPosition ?? new Point(work.Right - 150, work.Bottom);
        Player.FrameChanged += Present;
        Player.Error += error => { report(error); };
        Player.Completed += async () =>
        {
            if (stopped || IsDisposed) return;
            if (leaving) { wanted = false; Player.Stop(); Hide(); return; }
            manual = false;
            await PlayDefaultAsync();
        };
        behavior.Tick += async (_, _) =>
        {
            if (stopped || !Visible || behaviorPending) return;
            if (walking)
            {
                double fraction = Math.Clamp((time.Elapsed.TotalSeconds - walkStarted) / 1.2, 0, 1);
                anchor = new Point((int)Math.Round(walkStart.X + (walkEnd.X - walkStart.X) * fraction), walkStart.Y);
                Place(); Present();
                if (fraction >= 1) { walking = false; SaveAnchor(); await PlayDefaultAsync(); }
                return;
            }
            if (manual || dragging || thinking || speaking || leaving) return;
            string? current = Player.Current?.Name;
            if (current != "idle") return;
            behaviorPending = true;
            try
            {
                if (time.Elapsed.TotalSeconds - lastActivity >= 120) await EvaluateInactivityAsync(time.Elapsed.TotalSeconds - lastActivity);
                else if (time.Elapsed.TotalSeconds >= nextBlink) { ScheduleBlink(); await PlayAsync("blink"); }
            }
            finally { behaviorPending = false; }
        };
        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || stopped) return;
            dragging = true; walking = false; manual = false; leaving = false;
            dragOffset = e.Location; Capture = true; Touch(); _ = PlayAsync("dangle");
        };
        MouseMove += (_, e) =>
        {
            if (!dragging || stopped) return;
            Point pointer = PointToScreen(e.Location);
            anchor = new Point(pointer.X - dragOffset.X + Width / 2, pointer.Y - dragOffset.Y + Height);
            Place(); Present();
        };
        MouseUp += async (_, e) => { if (e.Button == MouseButtons.Left && dragging) await EndDragAsync(); };
        MouseCaptureChanged += async (_, _) => { if (dragging && !Capture) await EndDragAsync(); };
        MouseDoubleClick += async (_, e) => { if (e.Button == MouseButtons.Left && !stopped) await ReactAsync("grin"); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Prévisualiser les animations", null, (_, _) => openPreview());
        menu.Items.Add("Marcher à gauche (prototype)", null, async (_, _) => await WalkAsync(-120));
        menu.Items.Add("Marcher à droite (prototype)", null, async (_, _) => await WalkAsync(120));
        menu.Items.Add("Danser", null, async (_, _) => await ReactAsync("dance"));
        menu.Items.Add("Réveiller / revenir au repos", null, async (_, _) => await RestoreAsync());
        menu.Items.Add("Disparaître", null, async (_, _) => await ReactAsync("leave"));
        ContextMenuStrip = menu;
        FormClosed += (_, _) => { behavior.Dispose(); Player.Dispose(); menu.Dispose(); };
        ScheduleBlink();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { CreateParams parameters = base.CreateParams; parameters.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000; return parameters; }
    }
    internal static Point ClampAnchor(Point point, Size size, Rectangle work)
    {
        int width = Math.Min(size.Width, work.Width), height = Math.Min(size.Height, work.Height);
        return new Point(Math.Clamp(point.X, work.Left + width / 2, work.Right - (width - width / 2)), Math.Clamp(point.Y, work.Top + height, work.Bottom));
    }
    private void Place()
    {
        if (Player.CurrentFrame is not Bitmap frame) return;
        Rectangle work = Screen.FromPoint(anchor).WorkingArea;
        // A very small work area gets a proportionally fitted rendering as well.
        double fit = Math.Min(1, Math.Min((double)work.Width / frame.Width, (double)work.Height / frame.Height));
        Size size = new(Math.Max(1, (int)Math.Round(frame.Width * fit)), Math.Max(1, (int)Math.Round(frame.Height * fit)));
        anchor = ClampAnchor(anchor, size, work);
        Bounds = new Rectangle(anchor.X - size.Width / 2, anchor.Y - size.Height, size.Width, size.Height);
    }
    private void Present()
    {
        if (IsDisposed || Player.CurrentFrame is not Bitmap frame) return;
        Place();
        if (!nativeRendering || !Visible) return;
        try
        {
            if (ClientSize == frame.Size) LayeredSprite.Present(Handle, frame, Location);
            else
            {
                using var resized = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(resized)) graphics.DrawImage(frame, ClientRectangle);
                LayeredSprite.Present(Handle, resized, Location);
            }
        }
        catch (Exception error) { Stop(); Hide(); report("Affichage transparent du personnage impossible : " + error.Message); }
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if ((message.Msg == 0x007E || message.Msg == 0x02E0) && Player != null && !IsDisposed) { Place(); Present(); }
    }
    private void ScheduleBlink() => nextBlink = time.Elapsed.TotalSeconds + 3 + random.NextDouble() * 4;
    private void Touch() { lastActivity = time.Elapsed.TotalSeconds; ScheduleBlink(); }
    private void SaveAnchor() { try { savePosition(anchor); } catch (Exception) { report("Position du personnage non enregistrée."); } }
    private async Task EndDragAsync()
    {
        dragging = false; Capture = false; Touch(); SaveAnchor(); await PlayDefaultAsync();
    }
    private async Task PlayAsync(string name)
    {
        if (stopped || IsDisposed || !wanted) return;
        Task<bool> pending = Player.PlayAsync(name);
        int version = Player.RequestVersion;
        if (!await pending && Player.RequestVersion == version && !stopped && name != "idle" && !IsDisposed) await Player.PlayAsync("idle");
    }
    private Task PlayDefaultAsync() => PlayAsync(speaking ? "talk" : thinking ? "thinking" : "idle");
    public async Task ShowCharacterAsync(IWin32Window owner)
    {
        if (stopped || IsDisposed) return;
        wanted = true; leaving = false; manual = false; Touch();
        Task<bool> pending = Player.PlayAsync("arrive"); int version = Player.RequestVersion;
        bool loaded = await pending;
        if (stopped || !wanted || IsDisposed || Player.RequestVersion != version) return;
        if (!loaded && !await Player.PlayAsync("idle")) return;
        if (stopped || !wanted || IsDisposed) return;
        Place(); Show(owner); Present(); behavior.Start();
    }
    public async Task ReactAsync(string name)
    {
        if (stopped || !wanted || IsDisposed || !Visible) return;
        AnimationDefinition definition;
        try { definition = catalog.Get(name); } catch (Exception error) { report(error.Message); return; }
        if (definition.Overlay) { report("mouth est réservé à la prévisualisation isolée ; parole via talk, sans surcouche."); return; }
        manual = true; walking = false; leaving = name == "leave" || name == "perch_leave"; Touch();
        await PlayAsync(name);
    }
    public async Task RestoreAsync()
    {
        if (stopped || !wanted || IsDisposed) return;
        manual = false; leaving = false; walking = false; Touch();
        await PlayAsync(Player.Current?.Name == "sleep" ? "wake" : speaking ? "talk" : thinking ? "thinking" : "idle");
    }
    public async Task NotifyActivityAsync()
    {
        if (stopped || IsDisposed) return;
        Touch();
        if (!manual && Player.Current?.Name == "sleep") await PlayAsync("wake");
    }
    public async Task SetThinkingAsync(bool active)
    {
        if (thinking == active) return;
        thinking = active; Touch();
        if (!stopped && wanted && Visible && !manual && !dragging && !walking && !leaving) await PlayDefaultAsync();
    }
    public async Task SetSpeakingAsync(bool active)
    {
        if (speaking == active) return;
        speaking = active; Touch();
        if (!stopped && wanted && Visible && !dragging && !walking && !leaving) { manual = false; await PlayDefaultAsync(); }
    }
    internal async Task WalkAsync(int distance)
    {
        if (stopped || !wanted || IsDisposed) return;
        Touch(); manual = false; leaving = false; walking = false;
        walkStart = anchor;
        walkEnd = ClampAnchor(new Point(anchor.X + Math.Clamp(distance, -300, 300), anchor.Y), Size, Screen.FromPoint(anchor).WorkingArea);
        if (await Player.PlayAsync("walk") && !stopped) { walkStarted = time.Elapsed.TotalSeconds; walking = true; }
    }
    public async Task ApplyAppearanceAsync(int scale, bool onTop, bool visible, IWin32Window owner)
    {
        TopMost = onTop;
        if (!visible) { wanted = false; behavior.Stop(); Player.Stop(); Hide(); return; }
        int height = Math.Clamp(256 * scale / 100, 128, 512);
        if (!Visible) { Player.Height = height; await ShowCharacterAsync(owner); }
        else if (Player.Height != height)
        {
            Player.Height = height; await PlayAsync(Player.Current?.Name ?? "idle"); Place(); Present();
        }
    }
    internal async Task EvaluateInactivityAsync(double seconds)
    {
        // Inactivity is IRIS-local; no OS input inspection.
        if (seconds >= 120 && !stopped && !manual && !speaking && !thinking && !dragging && !walking && !leaving) await PlayAsync("sleep");
    }
    public void Stop() { stopped = true; wanted = false; dragging = false; walking = false; Capture = false; behavior.Stop(); Player.Stop(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { behavior.Dispose(); Player.Dispose(); }
        base.Dispose(disposing);
    }
}
