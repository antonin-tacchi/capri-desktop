using System;
using System.Drawing;
using System.Windows.Forms;

namespace IRIS.Desktop.Animation;

internal sealed class SpriteCanvas : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Bitmap? Frame { get; set; }
    public SpriteCanvas() { DoubleBuffered = true; Dock = DockStyle.Fill; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var light = new SolidBrush(Color.FromArgb(100, 100, 110));
        using var dark = new SolidBrush(Color.FromArgb(65, 65, 75));
        for (int y = 0; y < Height; y += 20)
            for (int x = 0; x < Width; x += 20) e.Graphics.FillRectangle((x / 20 + y / 20) % 2 == 0 ? dark : light, x, y, 20, 20);
        if (Frame == null) return;
        double ratio = Math.Min(1, Math.Min((double)Width / Frame.Width, (double)Height / Frame.Height));
        int w = (int)Math.Round(Frame.Width * ratio), h = (int)Math.Round(Frame.Height * ratio);
        e.Graphics.DrawImage(Frame, new Rectangle((Width - w) / 2, Height - h, w, h));
    }
}

internal sealed class AnimationPreviewWindow : Form
{
    internal AnimationPlayer Player { get; }
    private readonly ComboBox selection = new() { Name = "Animation", DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly Label details = new() { Name = "AnimationDetails", AutoSize = true, MaximumSize = new Size(760, 0) };
    private readonly Label status = new() { Name = "AnimationStatus", AutoSize = true, MaximumSize = new Size(760, 0) };
    private readonly Label frequency = new() { Name = "AnimationFrequency", AutoSize = true };
    private readonly SpriteCanvas canvas = new() { Name = "AnimationCanvas" };
    private readonly System.Windows.Forms.Timer diagnostics = new() { Interval = 500 };
    private readonly Button play = new() { Name = "PlayAnimation", Text = "Lire depuis le début", AutoSize = true };
    private readonly Button apply = new() { Name = "ApplyAnimation", Text = "Jouer sur le bureau", AutoSize = true };
    private int changes;
    private DateTime sampled = DateTime.UtcNow;

    public AnimationPreviewWindow(AnimationCatalog catalog, Action<string> applyToCharacter, Action restoreCharacter, bool stopped = false)
    {
        Text = "IRIS — Prévisualisation des animations (sans IA)";
        ClientSize = new Size(820, 740); MinimumSize = new Size(720, 650); Padding = new Padding(16);
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(23, 26, 49); ForeColor = Color.White;
        Player = new(catalog, 384);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 7; row++) layout.RowStyles.Add(new RowStyle(row == 3 ? SizeType.Percent : SizeType.AutoSize, row == 3 ? 100 : 0));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var pause = new Button { Name = "StopAnimation", Text = "Arrêter / figer", AutoSize = true };
        var restore = new Button { Name = "RestoreCharacter", Text = "Rétablir le comportement normal", AutoSize = true, Enabled = !stopped };
        apply.Enabled = !stopped;
        buttons.Controls.AddRange(new Control[] { selection, play, pause, apply, restore });
        layout.Controls.Add(buttons, 0, 0); layout.Controls.Add(details, 0, 1); layout.Controls.Add(frequency, 0, 2);
        layout.Controls.Add(canvas, 0, 3); layout.Controls.Add(status, 0, 4);
        layout.Controls.Add(new Label { Text = "Damier = transparence. mouth : calque isolé, jamais ajouté au personnage.\nMarche et poses perch : prototypes conservés tels quels, sans correction artificielle.", AutoSize = true }, 0, 5);
        var inventory = new TextBox { Name = "AnimationInventory", ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Height = 90,
            Text = $"{catalog.PngFiles.Count} PNG trouvés ; {catalog.Animations.Count} animations déclarées.\r\n" + string.Join("\r\n", catalog.Notices) };
        layout.Controls.Add(inventory, 0, 6); Controls.Add(layout);
        selection.DataSource = catalog.Animations;
        selection.SelectedItem = catalog.Get("idle");
        void Describe()
        {
            if (selection.SelectedItem is not AnimationDefinition definition) return;
            details.Text = $"{definition.Name} · {definition.Frames} images · {definition.Fps:0.##} FPS · boucle {(definition.Loop ? "oui" : "non")} · cellule {definition.FrameWidth} × {definition.FrameHeight}\n{definition.Description}";
            status.Text = definition.Error ?? "Sélectionnez Lire pour prévisualiser. Aucun moteur IA n’est démarré.";
            play.Enabled = definition.Error == null;
            apply.Enabled = !stopped && definition.Error == null && !definition.Overlay;
        }
        selection.SelectedIndexChanged += (_, _) => { Player.Stop(); Describe(); };
        Describe();
        play.Click += async (_, _) =>
        {
            if (selection.SelectedItem is not AnimationDefinition definition) return;
            status.Text = "Chargement de « " + definition.Name + " »…";
            if (await Player.PlayAsync(definition.Name)) { changes = 0; sampled = DateTime.UtcNow; status.Text = "Lecture : " + definition.Name; }
        };
        pause.Click += (_, _) => { Player.Stop(); status.Text = "Arrêtée sur l’image courante."; };
        apply.Click += (_, _) => { if (selection.SelectedItem is AnimationDefinition definition && !definition.Overlay) applyToCharacter(definition.Name); };
        restore.Click += (_, _) => restoreCharacter();
        Player.FrameChanged += () => { changes++; canvas.Frame = Player.CurrentFrame; canvas.Invalidate(); };
        Player.Completed += () => status.Text = "Lecture terminée ; dernière image conservée (sans boucle).";
        Player.Error += error => status.Text = error;
        diagnostics.Tick += (_, _) =>
        {
            double duration = (DateTime.UtcNow - sampled).TotalSeconds;
            frequency.Text = $"Image {Player.FrameIndex + 1}/{Player.Current?.Frames ?? 0} · changements affichés : {changes / Math.Max(0.001, duration):0.0}/s · {(Player.Playing ? "lecture" : "arrêt")} · images conservées {Player.RetainedBytes / 1048576.0:0.0} Mio";
            changes = 0; sampled = DateTime.UtcNow;
        };
        diagnostics.Start();
        FormClosed += (_, _) => { diagnostics.Dispose(); Player.Dispose(); canvas.Frame = null; };
    }
}
