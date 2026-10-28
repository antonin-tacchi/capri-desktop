using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IRIS.Desktop.Animation;

namespace IRIS.Desktop.Speech;

internal interface ISpeechBubble
{
    IReadOnlyList<string> Paginate(string text);
    void ShowPage(string text, int page, int total);
    void Hide();
    void Finish();
}

internal sealed class SpeechBubbleWindow : Form, ISpeechBubble
{
    private readonly BubbleArtwork artwork;
    private readonly Func<Rectangle> anchor;
    private readonly IWin32Window owner;
    private readonly System.Windows.Forms.Timer linger = new() { Interval = 3000 };
    private Bitmap? rendered;
    internal string DisplayedText { get; private set; } = "";
    internal string FontName => artwork.FontName;
    public event Action? StopRequested;

    public SpeechBubbleWindow(string assets, IWin32Window owner, Func<Rectangle> anchor)
    {
        this.anchor = anchor; this.owner = owner;
        Rectangle work = Screen.FromRectangle(anchor()).WorkingArea;
        artwork = new(assets, Math.Clamp(Math.Min(560, work.Width - 24), 300, 1000));
        Text = "IRIS — Bulle de discussion"; ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None; ClientSize = artwork.Size;
        linger.Tick += (_, _) => Hide();
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) StopRequested?.Invoke(); };
        var menu = new ContextMenuStrip(); menu.Items.Add("Arrêter la voix et fermer la bulle", null, (_, _) => StopRequested?.Invoke()); ContextMenuStrip = menu;
        FormClosed += (_, _) => menu.Dispose();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { CreateParams result = base.CreateParams; result.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000; return result; }
    }
    public IReadOnlyList<string> Paginate(string text) => artwork.Paginate(text);
    public void ShowPage(string text, int page, int total)
    {
        if (IsDisposed) return;
        linger.Stop();
        Bitmap next = artwork.Render(text, page, total); Bitmap? previous = rendered; rendered = next; previous?.Dispose();
        DisplayedText = text;
        Reposition();
        if (!Visible) Show(owner);
        Present();
    }
    public static Point Position(Rectangle person, Size bubble, Rectangle work)
    {
        int x = person.Left + person.Width / 2 - bubble.Width / 2;
        int above = person.Top - bubble.Height + 8;
        int y = above >= work.Top ? above : person.Bottom + bubble.Height + 8 <= work.Bottom ? person.Bottom + 8 : work.Top;
        return new(Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - bubble.Width)),
            Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - bubble.Height)));
    }
    public void Reposition()
    {
        if (IsDisposed) return;
        Rectangle person = anchor(); Rectangle work = Screen.FromRectangle(person).WorkingArea;
        double scale = Math.Min(1, Math.Min((work.Width - 16.0) / artwork.Size.Width, (work.Height - 16.0) / artwork.Size.Height));
        ClientSize = new Size(Math.Max(1, (int)(artwork.Size.Width * scale)), Math.Max(1, (int)(artwork.Size.Height * scale)));
        Location = Position(person, ClientSize, work);
        if (Visible) Present();
    }
    private void Present()
    {
        if (rendered == null || !Visible || IsDisposed) return;
        if (rendered.Size == ClientSize) LayeredSprite.Present(Handle, rendered, Location);
        else
        {
            using var fitted = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(fitted)) graphics.DrawImage(rendered, ClientRectangle);
            LayeredSprite.Present(Handle, fitted, Location);
        }
    }
    internal Bitmap? Snapshot() => rendered == null ? null : new Bitmap(rendered);
    public new void Hide() { linger.Stop(); base.Hide(); DisplayedText = ""; rendered?.Dispose(); rendered = null; }
    public void Finish() { if (!IsDisposed && Visible) linger.Start(); }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is 0x007E or 0x02E0 && artwork != null && !IsDisposed) Reposition();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { linger.Dispose(); rendered?.Dispose(); rendered = null; artwork.Dispose(); }
        base.Dispose(disposing);
    }
}
