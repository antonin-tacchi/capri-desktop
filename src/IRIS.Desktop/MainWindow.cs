using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Desktop.Animation;
using IRIS.Infrastructure;
using IRIS.Security.Policies;
using IRIS.AI.Conversation;
using IRIS.AI.Inference;
using IRIS.Desktop.Speech;
using System.IO;

namespace IRIS.Desktop;

internal sealed class MainWindow : Form
{
    private readonly LocalSettingsStore store;
    private readonly EmergencyStopService emergency;
    private readonly IHotkeyRegistrar hotkeys;
    private IrisSettings settings;
    private ActionPolicyEngine? policy;
    private readonly Label status = new() { Name = "Status", AutoSize = true, MaximumSize = new Size(580, 0) };
    private readonly Label character = new() { Name = "Character", Text = "◉", ForeColor = Color.Cyan, AutoSize = true };
    private readonly Button time = new() { Name = "Time", Text = "Heure", AutoSize = true };
    private readonly Button date = new() { Name = "Date", Text = "Date", AutoSize = true };
    private readonly Button converse = new() { Name = "Conversation", Text = "Conversation", AutoSize = true };
    private readonly Button memories = new() { Name = "Memory", Text = "Mémoire", AutoSize = true };
    private readonly Button voiceButton = new() { Name = "Voice", Text = "Voix", AutoSize = true };
    private readonly Label microphoneStatus = new() { Name = "MicrophoneStatus", Text = "Microphone : inactif · Capture écran : inactive", AutoSize = true };
    private bool hotkeyRegistered;
    private bool busy;
    private Font? characterFont;
    private ConversationWindow? chat;
    private MemoryWindow? memoryWindow;
    private VoiceWindow? voiceWindow;
    private bool closingVoice;
    private readonly AnimationCatalog? animations;
    private CharacterWindow? companion;
    private AnimationPreviewWindow? animationPreview;
    private readonly Button animationButton = new() { Name = "Animations", Text = "Animations", AutoSize = true };
    private readonly Label animationStatus = new() { Name = "CharacterStatus", AutoSize = true, MaximumSize = new Size(580, 0) };
    private readonly LocalActivityFilter activityFilter;
    private readonly VoiceServices? voiceServices;
    private SpeechBubbleWindow? speechBubble;
    private ReplySpeechController? replySpeech;

    public MainWindow(LocalSettingsStore store, IrisSettings settings, EmergencyStopService emergency, IHotkeyRegistrar? hotkeys = null, ILocalInferenceService? inference = null, IMemoryService? memory = null, VoiceServices? voice = null, AnimationCatalog? animations = null, string? animationError = null, string? engineRoot = null)
    {
        this.store = store;
        this.settings = settings;
        this.emergency = emergency;
        this.animations = animations;
        voiceServices = voice;
        activityFilter = new LocalActivityFilter(() => { if (companion != null) _ = companion.NotifyActivityAsync(); });
        this.hotkeys = hotkeys ?? new WindowsHotkeyRegistrar();
        Text = $"IRIS {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)} — Intelligent Responsive Interface System";
        ClientSize = new Size(690, 540);
        MinimumSize = new Size(620, 450);
        BackColor = Color.FromArgb(15, 18, 36);
        ForeColor = Color.FromArgb(220, 224, 245);
        Font = new Font("Segoe UI", 11);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        panel.Controls.Add(new Label { Text = "IRIS", AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.Cyan });
        panel.Controls.Add(character);
        panel.Controls.Add(new Label { Text = "IA locale à la demande · réseau limité à la boucle locale\nActions Windows : date et heure en lecture seule", AutoSize = true });
        panel.Controls.Add(microphoneStatus);
        var buttons = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(580, 0) };
        converse.Enabled = inference != null;
        var configure = new Button { Name = "Settings", Text = "Paramètres", AutoSize = true };
        var stop = new Button { Name = "EmergencyStop", Text = "ARRÊT D’URGENCE", AutoSize = true, BackColor = Color.DarkRed, ForeColor = Color.White };
        memories.Enabled = memory != null;
        voiceButton.Enabled = voice != null;
        animationButton.Enabled = animations != null;
        animationStatus.Text = animationError ?? (animations == null ? "Animations non configurées." : $"{animations.Animations.Count} animations disponibles · menu droit sur le personnage.");
        buttons.Controls.AddRange(new Control[] { time, date, converse, memories, voiceButton, animationButton, configure, stop });
        panel.Controls.Add(buttons);
        panel.Controls.Add(status);
        panel.Controls.Add(animationStatus);
        panel.Controls.Add(new Label { Text = "Souvenirs locaux sur ajout explicite ; partage IA séparé.\nVoix : consentement puis push-to-talk, sans envoi automatique.\nLa voix Windows est une solution de secours à évaluer.", AutoSize = true });
        Controls.Add(panel);
        animationButton.Click += (_, _) => OpenAnimationPreview();
        Shown += async (_, _) => await ApplyCharacterAsync();
        Application.AddMessageFilter(activityFilter);
        time.Click += async (_, _) => await RunClockAsync("clock.time");
        date.Click += async (_, _) => await RunClockAsync("clock.date");
        configure.Click += (_, _) => Configure();
        stop.Click += (_, _) => Stop();
        converse.Click += (_, _) =>
        {
            if (inference == null || emergency.IsStopped) return;
            if (chat is { IsDisposed: false }) { chat.Activate(); return; }
            chat = new ConversationWindow(inference, () => new PersonalityProfile(this.settings.Humor), emergency, Stop,
                message => { if (!IsDisposed) status.Text = message; }, memory,
                active => { if (companion != null) _ = companion.SetThinkingAsync(active); },
                expression => { if (companion != null) _ = companion.ReactAsync(expression); }, engineRoot: engineRoot,
                localClock: async (action, token) => await LocalClockQuery.ExecuteAsync(policy ?? throw new InvalidOperationException("Actions non initialisées."), action, token),
                speech: EnsureSpeech(), speakReplies: this.settings.SpeakReplies,
                saveSpeakReplies: enabled => { var next = this.settings with { SpeakReplies = enabled }; store.Save(next); this.settings = next; });
            chat.FormClosed += (_, _) => chat = null;
            chat.Show(this);
        };
        memories.Click += (_, _) =>
        {
            if (memory == null || policy == null || emergency.IsStopped) return;
            if (memoryWindow is { IsDisposed: false }) { memoryWindow.Activate(); return; }
            memoryWindow = new MemoryWindow(memory, policy, () => chat?.InvalidateMemory(), emergency);
            memoryWindow.FormClosed += (_, _) => memoryWindow = null;
            memoryWindow.Show(this);
        };
        voiceButton.Click += (_, _) =>
        {
            if (voice == null || emergency.IsStopped) return;
            if (voiceWindow is { IsDisposed: false }) { voiceWindow.Activate(); return; }
            ReplySpeechController? reader = EnsureSpeech();
            voiceWindow = new VoiceWindow(voice.Capture, voice.Recognizers, voice.Synthesis, voice.Playback, this.settings.Voice,
                preferences => { var next = this.settings with { Voice = preferences }; store.Save(next); this.settings = next; },
                text => { converse.PerformClick(); if (chat == null) throw new InvalidOperationException("Conversation indisponible."); chat.SetDraft(text); chat.Activate(); },
                emergency, Stop, active =>
                {
                    if (IsDisposed) return;
                    microphoneStatus.Text = active ? "MICROPHONE ACTIF OU ARRÊT NON CONFIRMÉ · fenêtre Voix" : "Microphone : inactif · Capture écran : inactive";
                    microphoneStatus.ForeColor = active ? Color.OrangeRed : ForeColor;
                }, active => { if (companion != null) _ = companion.SetSpeakingAsync(active); },
                beforeCapture: reader == null ? null : reader.StopAsync,
                readWithBubble: reader == null ? null : (text, preferences, progress, token) => reader.SpeakAsync(text, preferences, progress, token));
            voiceWindow.FormClosed += (_, _) => { voiceWindow = null; if (companion != null && replySpeech is not { IsBusy: true }) _ = companion.SetSpeakingAsync(false); };
            voiceWindow.Show(this);
        };
        FormClosing += async (_, e) =>
        {
            emergency.Stop();
            companion?.Stop();
            replySpeech?.Stop();
            animationPreview?.Close();
            if (!closingVoice && (voiceWindow is { IsDisposed: false } || replySpeech != null))
            {
                e.Cancel = true; closingVoice = true;
                try
                {
                    if (voiceWindow is { IsDisposed: false } current) await current.StopAndCloseAsync();
                    if (replySpeech != null) await replySpeech.StopAsync();
                }
                catch (Exception) { /* Closing the process releases any retained driver resources. */ }
                finally { Close(); }
            }
        };
        FormClosed += (_, _) => { Application.RemoveMessageFilter(activityFilter); companion?.Close(); companion?.Dispose(); companion = null; };
        ApplySettings();
    }

    public void SetPolicy(ActionPolicyEngine value) => policy = value;

    private async Task RunClockAsync(string name)
    {
        if (busy || emergency.IsStopped || policy == null) return;
        busy = true;
        time.Enabled = date.Enabled = false;
        try
        {
            var request = new ActionRequest(Guid.NewGuid(), Guid.NewGuid(), name, new Dictionary<string, string>(),
                RequestOrigin.UserInterface, "clock/Europe-Paris", DateTimeOffset.UtcNow.AddSeconds(30));
            ActionResult result = await policy.ExecuteAsync(request);
            if (!IsDisposed && !emergency.IsStopped) status.Text = result.Message;
        }
        finally
        {
            busy = false;
            if (!IsDisposed) time.Enabled = date.Enabled = !emergency.IsStopped;
        }
    }

    private void Configure()
    {
        using var dialog = new SettingsWindow(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { store.Save(dialog.Settings); settings = dialog.Settings; ApplySettings(); RegisterEmergencyHotkey(); _ = ApplyCharacterAsync(); }
        catch (Exception) { MessageBox.Show(this, "Enregistrement impossible. Vérifiez l’accès au dossier IRIS.", "IRIS", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ApplySettings()
    {
        TopMost = settings.AlwaysOnTop;
        if (speechBubble is { IsDisposed: false }) speechBubble.TopMost = settings.AlwaysOnTop;
        character.Visible = animations == null && settings.CharacterVisible;
        Font? previous = characterFont;
        characterFont = new Font("Segoe UI", 32f * settings.CharacterScale / 100f);
        character.Font = characterFont;
        previous?.Dispose();
    }

    internal void Stop()
    {
        emergency.Stop();
        time.Enabled = date.Enabled = false;
        converse.Enabled = false;
        memories.Enabled = false;
        voiceButton.Enabled = false;
        animationButton.Enabled = false;
        companion?.Stop();
        replySpeech?.Stop();
        animationPreview?.Close();
        status.Text = "Arrêt d’urgence actif. Les nouvelles actions sont bloquées jusqu’au redémarrage d’IRIS.";
    }

    private ReplySpeechController? EnsureSpeech()
    {
        if (voiceServices == null || emergency.IsStopped) return null;
        if (replySpeech != null) return replySpeech;
        try
        {
            speechBubble = new SpeechBubbleWindow(Path.Combine(AppContext.BaseDirectory, "assets"), this,
                () => companion is { Visible: true } ? companion.Bounds : Bounds) { TopMost = settings.AlwaysOnTop };
            replySpeech = new ReplySpeechController(voiceServices.Synthesis, voiceServices.Playback, speechBubble, () => settings.Voice,
                active => { if (companion != null && !IsDisposed) _ = companion.SetSpeakingAsync(active); }, Stop, emergency.Token);
            speechBubble.StopRequested += () => replySpeech?.Stop();
            return replySpeech;
        }
        catch (Exception error) { status.Text = "Bulle/lecture indisponible : " + error.Message; return null; }
    }

    private async System.Threading.Tasks.Task ApplyCharacterAsync()
    {
        if (animations == null || emergency.IsStopped || IsDisposed) return;
        if (companion == null)
        {
            Point? saved = settings.CharacterAnchorX is int x && settings.CharacterAnchorY is int y ? new Point(x, y) : null;
            companion = new CharacterWindow(animations, settings.CharacterScale, settings.AlwaysOnTop, saved,
                point => { var next = settings with { CharacterAnchorX = point.X, CharacterAnchorY = point.Y }; store.Save(next); settings = next; },
                message => { if (!IsDisposed) animationStatus.Text = message; }, OpenAnimationPreview);
            companion.LocationChanged += (_, _) => speechBubble?.Reposition();
            companion.SizeChanged += (_, _) => speechBubble?.Reposition();
            companion.VisibleChanged += (_, _) => speechBubble?.Reposition();
        }
        await companion.ApplyAppearanceAsync(settings.CharacterScale, settings.AlwaysOnTop, settings.CharacterVisible, this);
    }
    private void OpenAnimationPreview()
    {
        if (animations == null || emergency.IsStopped) return;
        if (animationPreview is { IsDisposed: false }) { animationPreview.Activate(); return; }
        animationPreview = new AnimationPreviewWindow(animations,
            async name =>
            {
                if (companion == null || !companion.Visible)
                {
                    if (!settings.CharacterVisible) { animationStatus.Text = "Activez l’affichage du personnage dans Paramètres."; return; }
                    await ApplyCharacterAsync();
                }
                if (companion != null) await companion.ReactAsync(name);
            }, async () => { if (companion is { Visible: false } && settings.CharacterVisible) await ApplyCharacterAsync(); if (companion != null) await companion.RestoreAsync(); });
        animationPreview.FormClosed += (_, _) => animationPreview = null;
        animationPreview.Show(this);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RegisterEmergencyHotkey(); }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (hotkeyRegistered) hotkeys.Unregister(Handle);
        hotkeyRegistered = false;
        base.OnHandleDestroyed(e);
    }
    private void RegisterEmergencyHotkey()
    {
        if (!IsHandleCreated) return;
        if (hotkeyRegistered) hotkeys.Unregister(Handle);
        hotkeyRegistered = hotkeys.Register(Handle, settings.EmergencyFunctionKey);
        if (!emergency.IsStopped) status.Text = hotkeyRegistered
            ? $"Arrêt d’urgence : Ctrl+Alt+F{settings.EmergencyFunctionKey}."
            : "Raccourci indisponible. Le bouton d’arrêt reste actif ; choisissez une autre touche dans les paramètres.";
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && message.WParam == new IntPtr(1)) Stop();
        base.WndProc(ref message);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { replySpeech?.Dispose(); speechBubble?.Dispose(); speechBubble = null; }
        base.Dispose(disposing);
        if (disposing) { characterFont?.Dispose(); characterFont = null; }
        if (disposing) Application.RemoveMessageFilter(activityFilter);
    }
}
