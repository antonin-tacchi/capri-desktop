using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Voice;

namespace IRIS.Desktop;

internal sealed class VoiceWindow : Form
{
    private readonly IAudioCapture capture;
    private readonly ITextToSpeechProvider synthesis;
    private readonly IReadOnlyList<ISpeechRecognitionProvider> recognizers;
    private readonly VoiceWorkflow workflow;
    private readonly Action<string> placeInChat;
    private readonly Action<VoicePreferences> save;
    private readonly Action emergencyStop;
    private readonly Action<bool>? reportMicrophone;
    private readonly EmergencyStopService emergency;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration emergencyRegistration;
    private CancellationTokenSource? operation;
    private CancellationTokenSource? release;
    private Task running = Task.CompletedTask;
    private bool busy, recording, driverFault;
    private bool allowClose;
    private bool spaceHeld;
    private readonly ToolTip hints = new() { AutoPopDelay = 10000 };
    private readonly ComboBox devices = new() { Name = "Microphone", DisplayMember = nameof(AudioInputDevice.Name), DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    private readonly ComboBox recognition = new() { Name = "Recognition", DisplayMember = nameof(ISpeechRecognitionProvider.Name), DropDownStyle = ComboBoxStyle.DropDownList, Width = 430 };
    private readonly ComboBox voices = new() { Name = "Voice", DisplayMember = nameof(LocalVoice.Name), DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly CheckBox consent = new() { Name = "MicrophoneConsent", Text = "Autoriser le microphone pour cette fenêtre, uniquement en maintenant le bouton", AutoSize = true };
    private readonly NumericUpDown gain = new() { Name = "Gain", Minimum = 50, Maximum = 200, Value = 100, Width = 65 };
    private readonly NumericUpDown threshold = new() { Name = "Threshold", Minimum = 0.1M, Maximum = 20, Value = 0.5M, DecimalPlaces = 1, Increment = 0.1M, Width = 65 };
    private readonly NumericUpDown volume = new() { Name = "Volume", Minimum = 0, Maximum = 100, Value = 80, Width = 65 };
    private readonly NumericUpDown rate = new() { Name = "Rate", Minimum = -5, Maximum = 5, Width = 65 };
    private readonly Button hold = new() { Name = "PushToTalk", Text = "Maintenir pour parler (ou Espace)", AutoSize = true };
    private readonly Button transfer = new() { Name = "PlaceInChat", Text = "Placer le texte dans le chat", AutoSize = true };
    private readonly Button speak = new() { Name = "Speak", Text = "Lire ce texte", AutoSize = true };
    private readonly Button testVoice = new() { Name = "TestVoice", Text = "Tester la voix", AutoSize = true };
    private readonly Button cancel = new() { Name = "CancelVoice", Text = "Annuler / arrêter la voix", AutoSize = true };
    private readonly Button refresh = new() { Name = "RefreshDevices", Text = "Actualiser", AutoSize = true };
    private readonly Button saveButton = new() { Name = "SaveVoice", Text = "Enregistrer les préférences", AutoSize = true };
    private readonly Label indicator = new() { Name = "MicrophoneIndicator", Text = "Microphone inactif", AutoSize = true, ForeColor = Color.Cyan };
    private readonly Label status = new() { Name = "Status", AutoSize = true, MaximumSize = new Size(850, 0) };
    private readonly ProgressBar meter = new() { Name = "AudioLevel", Width = 230, Maximum = 100, Height = 18 };
    private readonly Label signal = new() { Name = "AudioMetrics", Text = "Niveau du microphone : en attente de capture", AutoSize = true, MaximumSize = new Size(650, 0) };
    private readonly TextBox transcript = new() { Name = "Transcript", Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, MaxLength = 4096, PlaceholderText = "Transcription modifiable : vérifiez le texte avant de le placer dans le chat." };
    private readonly TextBox speechText = new() { Name = "SpeechText", Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, MaxLength = 700, Text = "Bonjour, je suis IRIS. La voix fonctionne localement et vous pouvez l’interrompre à tout moment." };
    private readonly VoicePreferences initial;
    private readonly Func<Task>? beforeCapture;
    private readonly Func<string, SpeechSynthesisSettings, IProgress<AudioLevel>?, CancellationToken, Task>? readWithBubble;

    public VoiceWindow(IAudioCapture capture, IReadOnlyList<ISpeechRecognitionProvider> recognizers, ITextToSpeechProvider synthesis,
        IAudioPlayback playback, VoicePreferences preferences, Action<VoicePreferences> save, Action<string> placeInChat,
        EmergencyStopService emergency, Action emergencyStop, Action<bool>? reportMicrophone = null, Action<bool>? reportSpeaking = null,
        Func<Task>? beforeCapture = null, Func<string, SpeechSynthesisSettings, IProgress<AudioLevel>?, CancellationToken, Task>? readWithBubble = null)
    {
        this.capture = capture; this.recognizers = recognizers; this.synthesis = synthesis; this.save = save; this.placeInChat = placeInChat;
        this.emergencyStop = emergencyStop; initial = preferences; preferences.Validate();
        workflow = new(capture, synthesis, new ObservedAudioPlayback(playback, active => Dispatch(() => reportSpeaking?.Invoke(active))));
        this.reportMicrophone = reportMicrophone;
        this.emergency = emergency;
        this.beforeCapture = beforeCapture; this.readWithBubble = readWithBubble;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(emergency.Token);
        Text = "IRIS — Voix française locale"; ClientSize = new Size(960, 740); MinimumSize = new Size(900, 700);
        BackColor = Color.FromArgb(23, 26, 49); ForeColor = Color.White; Font = new Font("Segoe UI", 10); Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 12; row++) layout.RowStyles.Add(new RowStyle(row == 6 ? SizeType.Percent : row == 9 ? SizeType.Absolute : SizeType.AutoSize, row == 6 ? 100 : row == 9 ? 80 : 0));
        layout.Controls.Add(new Label { Text = "Aucune écoute permanente · audio en mémoire uniquement · transcription à vérifier · aucun envoi automatique", AutoSize = true }, 0, 0);
        layout.Controls.Add(Row(new Label { Text = "Microphone", AutoSize = true }, devices, refresh), 0, 1);
        layout.Controls.Add(Row(new Label { Text = "Reconnaissance", AutoSize = true }, recognition), 0, 2);
        layout.Controls.Add(consent, 0, 3);
        layout.Controls.Add(Row(new Label { Text = "Gain (%)", AutoSize = true }, gain, new Label { Text = "Seuil parole (%)", AutoSize = true }, threshold, hold, indicator), 0, 4);
        layout.Controls.Add(Row(meter, signal), 0, 5); layout.Controls.Add(transcript, 0, 6);
        layout.Controls.Add(Row(transfer, new Label { Text = "L’envoi au modèle reste un clic séparé dans le chat.", AutoSize = true }), 0, 7);
        layout.Controls.Add(Row(new Label { Text = "Voix française locale", AutoSize = true }, voices, new Label { Text = "Volume", AutoSize = true }, volume, new Label { Text = "Vitesse", AutoSize = true }, rate), 0, 8);
        layout.Controls.Add(speechText, 0, 9);
        var stop = new Button { Name = "EmergencyStop", Text = "ARRÊT D’URGENCE", AutoSize = true, BackColor = Color.DarkRed, ForeColor = Color.White };
        layout.Controls.Add(Row(testVoice, speak, cancel, saveButton, stop), 0, 10); layout.Controls.Add(status, 0, 11); Controls.Add(layout);
        gain.Value = preferences.GainPercent; threshold.Value = preferences.ThresholdPermille / 10M; volume.Value = preferences.Volume; rate.Value = preferences.Rate;
        consent.CheckedChanged += (_, _) => { if (!consent.Checked && recording) operation?.Cancel(); UpdateState(); };
        recognition.SelectedIndexChanged += (_, _) => UpdateState(); devices.SelectedIndexChanged += (_, _) => UpdateState(); transcript.TextChanged += (_, _) => UpdateState();
        hold.MouseDown += async (_, e) => { if (e.Button == MouseButtons.Left) { hold.Capture = true; await BeginCaptureAsync(); } };
        hold.MouseUp += (_, _) => EndCapture();
        hold.MouseCaptureChanged += (_, _) => Dispatch(() => { if (!hold.Capture && recording && release is { IsCancellationRequested: false }) operation?.Cancel(); });
        hold.LostFocus += (_, _) => { spaceHeld = false; if (recording && release is { IsCancellationRequested: false }) operation?.Cancel(); };
        hold.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Space) return;
            e.SuppressKeyPress = true;
            if (!spaceHeld) { spaceHeld = true; await BeginCaptureAsync(); }
        };
        hold.KeyUp += (_, e) => { if (e.KeyCode == Keys.Space) { e.SuppressKeyPress = true; spaceHeld = false; EndCapture(); } };
        Deactivate += (_, _) => { if (recording) operation?.Cancel(); };
        cancel.Click += (_, _) => operation?.Cancel();
        stop.Click += (_, _) => emergencyStop();
        refresh.Click += async (_, _) => await StartOperationAsync(LoadDevicesAsync);
        speak.Click += async (_, _) => await SpeakAsync(speechText.Text);
        testVoice.Click += async (_, _) => await SpeakAsync("Bonjour, je suis IRIS. Je vais parler tranquillement, en respectant les pauses. Le texte apparaît pendant que je parle.");
        transfer.Click += (_, _) =>
        {
            try { placeInChat(transcript.Text.Trim()); status.Text = "Texte placé dans le brouillon. Vérifiez-le puis cliquez Envoyer dans le chat."; }
            catch (InvalidOperationException error) { status.Text = error.Message; }
        };
        saveButton.Click += (_, _) =>
        {
            try
            {
                var value = new VoicePreferences((devices.SelectedItem as AudioInputDevice)?.Id, (recognition.SelectedItem as ISpeechRecognitionProvider)?.Id,
                    (voices.SelectedItem as LocalVoice)?.Id, (int)gain.Value, (int)(threshold.Value * 10), (int)volume.Value, (int)rate.Value,
                    (voices.SelectedItem as LocalVoice)?.Id.StartsWith("piper-", StringComparison.Ordinal) == true);
                value.Validate(); save(value); status.Text = "Préférences enregistrées. L’autorisation du micro n’est pas conservée.";
            }
            catch (Exception) { status.Text = "Enregistrement des préférences vocales impossible."; }
        };
        Shown += async (_, _) => await StartOperationAsync(LoadDevicesAsync);
        FormClosing += async (_, e) =>
        {
            lifetime.Cancel(); operation?.Cancel(); release?.Cancel();
            if (busy && !allowClose) { e.Cancel = true; await StopAndCloseAsync(); }
        };
        FormClosed += (_, _) => { emergencyRegistration.Dispose(); lifetime.Dispose(); hints.Dispose(); if (!driverFault) reportMicrophone?.Invoke(false); };
        emergencyRegistration = emergency.Token.Register(() => Dispatch(UpdateState));
        UpdateState();
    }
    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        row.Controls.AddRange(controls); return row;
    }
    private void Dispatch(Action action)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => { if (!IsDisposed) action(); })); }
        catch (InvalidOperationException) { }
    }
    private async Task LoadDevicesAsync(CancellationToken token)
    {
        string? selected = (devices.SelectedItem as AudioInputDevice)?.Id ?? initial.MicrophoneId;
        var inventory = await Task.Run(() => (Inputs: capture.GetDevices(), Voices: synthesis.GetVoices()), token);
        if (IsDisposed) return;
        devices.DataSource = inventory.Inputs.ToList(); voices.DataSource = inventory.Voices.ToList(); recognition.DataSource = recognizers.ToList();
        if (selected != null)
        {
            devices.SelectedItem = inventory.Inputs.FirstOrDefault(d => d.Id == selected);
            if (devices.SelectedItem == null) consent.Checked = false;
        }
        voices.SelectedItem = (initial.PreferNatural ? inventory.Voices.FirstOrDefault(v => v.Id.StartsWith("piper-", StringComparison.Ordinal)) : null)
            ?? inventory.Voices.FirstOrDefault(v => v.Id == initial.VoiceId) ?? inventory.Voices.FirstOrDefault();
        recognition.SelectedItem = recognizers.FirstOrDefault(r => r.Id == initial.RecognitionId && r.IsAvailable) ?? recognizers.FirstOrDefault(r => r.IsAvailable);
        status.Text = "Choisissez le périphérique et le moteur. Le microphone reste inactif sans consentement et maintien du bouton.";
    }
    internal Task BeginCaptureAsync()
    {
        if (busy || !consent.Checked || lifetime.IsCancellationRequested || devices.SelectedItem is not AudioInputDevice input || recognition.SelectedItem is not ISpeechRecognitionProvider provider) return Task.CompletedTask;
        return BeginCoreAsync(input, provider);
    }
    private async Task BeginCoreAsync(AudioInputDevice input, ISpeechRecognitionProvider provider)
    {
        using var released = new CancellationTokenSource(); release = released;
        recording = true;
        try
        {
            await StartOperationAsync(async token =>
            {
                if (beforeCapture != null) await beforeCapture();
                token.ThrowIfCancellationRequested();
                SpeechSignalAnalysis? analysis = null;
                signal.Text = "Mesure du niveau d’entrée…";
                status.Text = "Capture demandée sur « " + input.Name + " ». Relâchez pour terminer (30 s maximum).";
                SpeechRecognitionResult result = await workflow.CaptureAndRecognizeAsync(consent.Checked, input.Id, new((double)gain.Value / 100, (double)threshold.Value / 100),
                    provider, LevelProgress(), () => Dispatch(() => { if (recording) { recording = false; if (busy) status.Text = "Microphone fermé · transcription française locale…"; UpdateState(); } }), released.Token, token,
                    report => analysis = report);
                token.ThrowIfCancellationRequested();
                if (IsDisposed) return;
                transcript.Text = result.Text;
                status.Text = result.Text.Length == 0 ? "Aucune parole reconnue. Vérifiez le microphone, le gain et le seuil." : "Texte reconnu à corriger si nécessaire" + (result.Confidence is double confidence ? $" · confiance du moteur {confidence:P0}" : " · confiance non fournie par le moteur");
                if (analysis != null)
                {
                    signal.Text = $"Prise {analysis.Duration.TotalSeconds:0.0} s · " + (analysis.TooQuiet ? "signal trop faible" : analysis.Clipped ? "signal saturé" : analysis.Rms < 0.005 ? "signal faible, niveau ajusté pour transcrire" : "niveau correct");
                    hints.SetToolTip(signal, $"RMS {analysis.Rms:P2} · crête {analysis.Peak:P0} · gain de préparation ASR ×{analysis.RecognitionGain:0.0}");
                    if (analysis.TooQuiet) status.Text = "Signal trop faible : vérifiez le périphérique et le niveau du micro dans Windows. Aucune transcription envoyée.";
                    else if (analysis.Clipped) status.Text += " · Signal saturé : baissez le gain ou le niveau du micro dans Windows.";
                    else if (analysis.Rms < 0.005) status.Text += " · Signal faible : rapprochez le microphone ou ajustez son niveau dans Windows.";
                }
            });
        }
        finally { recording = false; release = null; if (!IsDisposed) UpdateState(); }
    }
    internal void EndCapture() => release?.Cancel();
    private Task SpeakAsync(string text) => StartOperationAsync(async token =>
    {
        LocalVoice voice = voices.SelectedItem as LocalVoice ?? throw new InvalidOperationException("Aucune voix féminine française disponible.");
        status.Text = "Synthèse et lecture locales…";
        var settings = new SpeechSynthesisSettings(voice.Id, (int)volume.Value, (int)rate.Value);
        if (readWithBubble != null) await readWithBubble(text, settings, LevelProgress(), token);
        else await workflow.SpeakAsync(text, settings, LevelProgress(), token);
        token.ThrowIfCancellationRequested();
        if (!IsDisposed) status.Text = "Lecture terminée.";
    });
    private IProgress<AudioLevel> LevelProgress() => new Progress<AudioLevel>(level =>
    {
        if (IsDisposed || !busy || driverFault || lifetime.IsCancellationRequested) return;
        meter.Value = level.Rms <= 0 ? 0 : (int)Math.Clamp(100 + Math.Log10(level.Rms) * 100 / 3, 0, 100);
        if (recording) signal.Text = $"{level.Elapsed.TotalSeconds:0.0} s · " + (level.Clipped ? "saturation : baissez le gain" : level.Rms < 0.005 ? "signal faible" : "niveau correct");
        if (level.Clipped) status.Text = "Signal saturé : réduisez le gain d’entrée.";
    });
    private Task StartOperationAsync(Func<CancellationToken, Task> work)
    {
        if (busy || lifetime.IsCancellationRequested || driverFault) return Task.CompletedTask;
        running = ExecuteAsync(work); return running;
    }
    private async Task ExecuteAsync(Func<CancellationToken, Task> work)
    {
        busy = true; using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); operation = request; UpdateState();
        try { await work(request.Token); }
        catch (AudioDriverShutdownException error) { driverFault = true; if (!IsDisposed) status.Text = error.Message; reportMicrophone?.Invoke(true); emergencyStop(); }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Opération vocale annulée ; aucun texte envoyé."; }
        catch (Exception error)
        {
            if (!IsDisposed) status.Text = error is InvalidOperationException or ArgumentException ? error.Message : "Traitement vocal impossible. Vérifiez le périphérique et les fichiers du moteur.";
        }
        finally { busy = false; operation = null; if (!IsDisposed) { meter.Value = 0; UpdateState(); } }
    }
    private void UpdateState()
    {
        bool available = !busy && !lifetime.IsCancellationRequested && !driverFault;
        bool recognitionAvailable = recognition.SelectedItem is ISpeechRecognitionProvider provider && provider.IsAvailable;
        hold.Enabled = !lifetime.IsCancellationRequested && !driverFault && (recording || (available && consent.Checked && devices.SelectedItem != null && recognitionAvailable));
        transfer.Enabled = available && !string.IsNullOrWhiteSpace(transcript.Text);
        speak.Enabled = testVoice.Enabled = available && voices.SelectedItem != null;
        refresh.Enabled = saveButton.Enabled = devices.Enabled = recognition.Enabled = voices.Enabled = gain.Enabled = threshold.Enabled = volume.Enabled = rate.Enabled = available;
        transcript.Enabled = speechText.Enabled = available;
        consent.Enabled = !lifetime.IsCancellationRequested && !driverFault;
        cancel.Enabled = busy && !lifetime.IsCancellationRequested;
        indicator.Text = driverFault ? "Audio potentiellement actif : quittez IRIS" : recording && busy ? "MICROPHONE ACTIF" : "Microphone inactif";
        indicator.ForeColor = driverFault || (recording && busy) ? Color.OrangeRed : Color.Cyan;
        reportMicrophone?.Invoke(driverFault || (recording && busy));
        if (lifetime.IsCancellationRequested && !driverFault) status.Text = emergency.IsStopped ? "Arrêt d’urgence actif. Redémarrez IRIS avant une nouvelle opération." : "Arrêt de la fenêtre vocale en cours…";
    }
    internal async Task StopAndCloseAsync()
    {
        if (IsDisposed) return;
        lifetime.Cancel(); operation?.Cancel(); release?.Cancel();
        await running;
        allowClose = true;
        if (!IsDisposed) Close();
    }
}
