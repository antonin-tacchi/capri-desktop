using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.AI.Conversation;
using IRIS.AI.Inference;
using IRIS.Core.Services;
using IRIS.Core.Contracts;
using IRIS.Memory.Storage;
using IRIS.Infrastructure;
using System.IO;
using IRIS.Desktop.Speech;

namespace IRIS.Desktop;

internal sealed class ConversationWindow : Form
{
    private readonly ILocalInferenceService inference;
    private readonly ConversationService conversation;
    private readonly Func<PersonalityProfile> personality;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration emergencyRegistration;
    private CancellationTokenSource? operation;
    private bool busy;
    private readonly Action<bool>? reportThinking;
    private readonly Action<string>? reportExpression;
    private readonly RichTextBox transcript = new() { Name = "Transcript", ReadOnly = true, DetectUrls = false, Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 18, 36), ForeColor = Color.White };
    private readonly TextBox input = new() { Name = "Input", AccessibleName = "Votre message", Dock = DockStyle.Fill, MaxLength = 4096,
        Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, PlaceholderText = "Écrivez votre message ici…", BackColor = Color.White, ForeColor = Color.FromArgb(20, 20, 30) };
    private readonly Label status = new() { Name = "Status", AutoSize = true, MaximumSize = new Size(700, 0) };
    private string progressText = "Traitement local…";
    private readonly string? engineRoot;
    private readonly Func<string, CancellationToken, Task<string>>? localClock;
    private readonly ReplySpeechController? speech;
    private string? lastReply;
    private string? speechError;
    private readonly CheckBox readReplies = new() { Name = "ReadReplies", Text = "Lire les réponses à voix haute", AutoSize = true };
    private readonly Button replay = new() { Name = "ReplayReply", Text = "Relire", AutoSize = true };
    private readonly Button load = new() { Name = "Load", Text = "Charger IA", AutoSize = true };
    private readonly Button unload = new() { Name = "Unload", Text = "Décharger IA", AutoSize = true };
    private readonly Button send = new() { Name = "Send", Text = "Envoyer", AutoSize = true };
    private readonly Button cancel = new() { Name = "Cancel", Text = "Interrompre", AutoSize = true };
    private readonly Button clear = new() { Name = "Clear", Text = "Effacer la session", AutoSize = true };
    private readonly CheckBox cpu = new() { Name = "CpuOnly", Text = "CPU uniquement", AutoSize = true };

    public ConversationWindow(ILocalInferenceService inference, Func<PersonalityProfile> personality, EmergencyStopService emergency, Action emergencyStop, Action<string>? reportError = null, IMemoryContextProvider? memory = null, Action<bool>? reportThinking = null, Action<string>? reportExpression = null, string? engineRoot = null, Func<string, CancellationToken, Task<string>>? localClock = null, ReplySpeechController? speech = null, bool speakReplies = true, Action<bool>? saveSpeakReplies = null)
    {
        this.inference = inference;
        this.personality = personality;
        this.reportThinking = reportThinking;
        this.reportExpression = reportExpression;
        this.engineRoot = engineRoot;
        this.localClock = localClock;
        this.speech = speech;
        readReplies.Checked = speech != null && speakReplies;
        conversation = new(inference, memory);
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(emergency.Token);
        Text = "IRIS — Conversation locale";
        ClientSize = new Size(800, 640);
        MinimumSize = new Size(720, 540);
        BackColor = Color.FromArgb(23, 26, 49);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Conversation locale · souvenirs seulement si autorisés dans Mémoire · aucun outil Windows", AutoSize = true }, 0, 0);
        var modelControls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        modelControls.Controls.AddRange(new Control[] { load, unload, cpu, status });
        if (engineRoot != null)
        {
            var diagnostic = new Button { Name = "ModelDiagnostics", Text = "Diagnostic IA", AutoSize = true };
            diagnostic.Click += (_, _) =>
            {
                var missing = EngineAssets.Missing(engineRoot, false);
                MessageBox.Show(this, "Dossier utilisé :\n" + engineRoot + "\n\n" + (missing.Count == 0
                    ? "Fichiers principaux présents. Leur intégrité sera vérifiée au chargement."
                    : "Fichiers absents :\n" + string.Join("\n", missing)), "IRIS — Diagnostic IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            modelControls.Controls.Add(diagnostic);
        }
        layout.Controls.Add(modelControls, 0, 1);
        layout.Controls.Add(transcript, 0, 2);
        layout.Controls.Add(new Label { Text = "Votre message · Entrée pour envoyer · Maj+Entrée pour une nouvelle ligne", AutoSize = true }, 0, 3);
        layout.Controls.Add(input, 0, 4);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var stop = new Button { Name = "EmergencyStop", Text = "ARRÊT D’URGENCE", AutoSize = true, BackColor = Color.DarkRed, ForeColor = Color.White };
        actions.Controls.AddRange(new Control[] { send, cancel, clear, readReplies, replay, stop });
        layout.Controls.Add(actions, 0, 5);
        Controls.Add(layout);
        load.Click += async (_, _) => await ExecuteAsync(async token => { await inference.StartAsync(cpu.Checked, token); }, "Chargement IA : vérification des fichiers puis démarrage… Vous pouvez préparer votre message.");
        unload.Click += async (_, _) => await ExecuteAsync(_ => inference.StopAsync());
        send.Click += async (_, _) => await SendAsync();
        input.TextChanged += (_, _) => UpdateState(false);
        input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || e.Shift) return;
            e.SuppressKeyPress = true;
            if (!busy && !lifetime.IsCancellationRequested && (inference.IsReady || CanAnswerClock())) await SendAsync();
            else if (!busy && !lifetime.IsCancellationRequested) status.Text = "Message conservé. Cliquez Charger IA, puis Envoyer.";
        };
        Shown += (_, _) => input.Focus();
        cancel.Click += (_, _) => { operation?.Cancel(); speech?.Stop(); };
        clear.Click += async (_, _) => await ExecuteAsync(async token => { speech?.Stop(); lastReply = null; await conversation.ClearAsync(token); transcript.Clear(); });
        readReplies.CheckedChanged += (_, _) =>
        {
            speechError = null;
            if (!readReplies.Checked) speech?.Stop();
            try { saveSpeakReplies?.Invoke(readReplies.Checked); }
            catch (Exception) { status.Text = "Préférence de lecture non enregistrée."; }
        };
        replay.Click += (_, _) => { if (lastReply != null) _ = ReadReplyAsync(lastReply); };
        if (speech != null) speech.StateChanged += OnSpeechStateChanged;
        stop.Click += (_, _) => { emergencyStop(); UpdateState(); };
        FormClosing += (_, _) => { lifetime.Cancel(); operation?.Cancel(); speech?.Stop(); };
        FormClosed += async (_, _) =>
        {
            reportThinking?.Invoke(false);
            if (speech != null) speech.StateChanged -= OnSpeechStateChanged;
            lastReply = null;
            try { await inference.StopAsync(); }
            catch (Exception) { reportError?.Invoke("Arrêt du moteur incomplet. Fermez IRIS et vérifiez les diagnostics avant de relancer l’IA."); }
            finally { emergencyRegistration.Dispose(); lifetime.Dispose(); }
        };
        emergencyRegistration = emergency.Token.Register(() =>
        {
            try { if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) UpdateState(); })); }
            catch (InvalidOperationException) { }
        });
        UpdateState();
    }

    private async Task SendAsync()
    {
        string submittedDraft = input.Text;
        string text = submittedDraft.Trim();
        string? clock = localClock != null ? LocalClockQuery.Match(text) : null;
        if (text.Length == 0 || (!inference.IsReady && clock == null)) return;
        speech?.Stop();
        await ExecuteAsync(async token =>
        {
            string answer;
            reportThinking?.Invoke(true);
            try { answer = clock != null ? await localClock!(clock, token) : await conversation.SendAsync(text, personality(), token); }
            finally { reportThinking?.Invoke(false); }
            if (IsDisposed || token.IsCancellationRequested) return;
            transcript.AppendText("Vous : " + text + Environment.NewLine + "IRIS : " + answer + Environment.NewLine + Environment.NewLine);
            if (transcript.TextLength > 40000) transcript.Text = transcript.Text[^32000..];
            if (input.Text == submittedDraft) input.Clear();
            reportExpression?.Invoke("happy");
            lastReply = answer;
            if (readReplies.Checked) _ = ReadReplyAsync(answer);
        }, "Réponse locale en cours… Vous pouvez rédiger le message suivant.");
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> work, string progress = "Traitement local…")
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true;
        progressText = progress;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operation = request;
        bool failed = false;
        UpdateState();
        try { await work(request.Token); }
        catch (OperationCanceledException) { failed = true; if (!IsDisposed) status.Text = "Opération interrompue. Rechargez l’IA si nécessaire."; }
        catch (Exception error)
        {
            failed = true;
            reportExpression?.Invoke("sad");
            if (!IsDisposed)
                status.Text = error is MemoryContextChangedException ? error.Message : error is MemoryStorageException
                    ? "Mémoire indisponible : vérifiez le panneau Mémoire avant de relancer votre message."
                    : error is System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException
                    ? "Fichier IA absent : " + (error is FileNotFoundException file ? file.FileName ?? file.Message : error.Message) + " · Diagnostic IA indique le dossier utilisé."
                    : error is InvalidDataException ? "Intégrité des fichiers IA : " + error.Message
                    : error is System.ComponentModel.Win32Exception native ? "Démarrage du moteur impossible (Windows " + native.NativeErrorCode + "). Essayez CPU uniquement."
                    : error is IOException ? "Moteur local : " + error.Message
                    : "Échec du moteur local (" + error.GetType().Name + "). Essayez le mode CPU ou rechargez l’IA.";
        }
        finally
        {
            operation = null;
            busy = false;
            if (!IsDisposed) UpdateState(!failed);
        }
    }

    internal void InvalidateMemory()
    {
        speech?.Stop(); lastReply = null;
        operation?.Cancel();
        transcript.Clear();
        status.Text = "Mémoire modifiée : contexte de conversation réinitialisé.";
    }
    internal void SetDraft(string text)
    {
        if (lifetime.IsCancellationRequested) throw new InvalidOperationException("Le chat est arrêté ; redémarrez IRIS avant de préparer un message.");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096) throw new InvalidOperationException("Le texte vocal doit contenir de 1 à 4096 caractères.");
        if (input.Text.Length > 0 && input.Text != text) throw new InvalidOperationException("Le chat contient déjà un brouillon. Conservez-le ou effacez-le avant de placer le texte vocal.");
        input.Text = text;
    }

    private void UpdateState(bool updateText = true)
    {
        bool stopped = lifetime.IsCancellationRequested;
        load.Enabled = !busy && !stopped && !inference.IsReady;
        unload.Enabled = !busy && !stopped && inference.IsReady;
        send.Enabled = !busy && !stopped && (inference.IsReady || CanAnswerClock());
        clear.Enabled = !busy && !stopped;
        cancel.Enabled = (busy || speech is { IsBusy: true }) && !stopped;
        replay.Enabled = speech != null && lastReply != null && !stopped;
        readReplies.Enabled = speech != null && !stopped;
        cpu.Enabled = !busy && !inference.IsReady && !stopped;
        input.Enabled = !stopped;
        if (stopped) status.Text = "Arrêt d’urgence actif.";
        else if (updateText) status.Text = busy ? progressText : speech is { IsBusy: true } ? "Lecture de la réponse… Interrompre pour arrêter la voix." : inference.IsReady ? "IA locale prête." : "IA arrêtée : rédigez votre message, puis cliquez Charger IA et Envoyer.";
    }
    private void OnSpeechStateChanged()
    {
        if (IsDisposed) return;
        UpdateState(!busy && speechError == null);
        if (!busy && speechError != null) status.Text = speechError;
    }
    private async Task ReadReplyAsync(string text)
    {
        if (speech == null || lifetime.IsCancellationRequested) return;
        speechError = null;
        try { await speech.SpeakAsync(text, token: lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { speechError = "Réponse reçue ; lecture vocale impossible : " + error.Message; if (!IsDisposed) status.Text = speechError; }
        finally { if (!IsDisposed) UpdateState(!busy && speechError == null); }
    }
    private bool CanAnswerClock() => localClock != null && LocalClockQuery.Match(input.Text) != null;
}
