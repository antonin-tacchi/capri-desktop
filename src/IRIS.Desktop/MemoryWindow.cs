using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Memory;
using IRIS.Security.Policies;

namespace IRIS.Desktop;

internal sealed class MemoryWindow : Form
{
    private readonly IMemoryService memory;
    private readonly ActionPolicyEngine policy;
    private readonly Action changed;
    private readonly CancellationTokenSource lifetime;
    private readonly CheckBox enabled = new() { Name = "MemoryEnabled", Text = "Autoriser la mémorisation explicite", AutoSize = true };
    private readonly CheckBox share = new() { Name = "ShareMemory", Text = "Utiliser ces souvenirs dans les réponses de l’IA locale", AutoSize = true };
    private readonly NumericUpDown retention = new() { Name = "RetentionDays", Minimum = 1, Maximum = 3650, Value = 90, Width = 80 };
    private readonly TextBox search = new() { Name = "Search", Width = 220, MaxLength = 200 };
    private readonly TextBox title = new() { Name = "MemoryTitle", PlaceholderText = "Titre du souvenir (80 caractères maximum)", Dock = DockStyle.Fill, MaxLength = 80 };
    private readonly TextBox content = new() { Name = "MemoryContent", PlaceholderText = "Information à conserver explicitement, sans secret (1000 caractères maximum)", Dock = DockStyle.Fill, Multiline = true, MaxLength = 1000, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox kind = new() { Name = "MemoryKind", DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly Label status = new() { Name = "Status", AutoSize = true };
    private readonly DataGridView grid = new()
    {
        Name = "Memories", Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(15, 18, 36)
    };
    private readonly List<Button> buttons = new();
    private bool busy;
    private bool active;
    private MemoryItem? Selected => grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].DataBoundItem as MemoryItem : null;

    public MemoryWindow(IMemoryService memory, ActionPolicyEngine policy, Action changed, EmergencyStopService emergency)
    {
        this.memory = memory; this.policy = policy; this.changed = changed;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(emergency.Token);
        Text = "IRIS — Mémoire locale";
        ClientSize = new Size(900, 710); MinimumSize = new Size(800, 660);
        BackColor = Color.FromArgb(23, 26, 49); ForeColor = Color.White; Font = new Font("Segoe UI", 10);
        Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 8; row++) layout.RowStyles.Add(new RowStyle(row == 3 ? SizeType.Percent : row == 5 ? SizeType.Absolute : SizeType.AutoSize, row == 3 ? 100 : row == 5 ? 90 : 0));
        layout.Controls.Add(new Label { Text = "Aucune conversation enregistrée automatiquement. N’ajoutez aucun mot de passe, clé ou donnée sensible.", AutoSize = true }, 0, 0);
        var choices = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        choices.Controls.Add(enabled); choices.Controls.Add(share);
        var retentionPanel = new FlowLayoutPanel { AutoSize = true };
        retentionPanel.Controls.AddRange(new Control[] { new Label { Text = "Conservation (jours)", AutoSize = true }, retention, Button("ApplyPolicy", "Appliquer les choix", ApplyPolicyAsync) });
        choices.Controls.Add(retentionPanel); layout.Controls.Add(choices, 0, 1);
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        tools.Controls.AddRange(new Control[] { search, Button("SearchButton", "Rechercher", ReloadAsync), Button("NewMemory", "Nouveau", _ => { grid.ClearSelection(); title.Clear(); content.Clear(); kind.SelectedIndex = 0; return Task.CompletedTask; }),
            Button("Export", "Exporter JSON en clair", ExportAsync), Button("Import", "Importer JSON", ImportAsync) });
        layout.Controls.Add(tools, 0, 2);
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Titre", DataPropertyName = nameof(MemoryItem.Title), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Catégorie", DataPropertyName = nameof(MemoryItem.Kind), Width = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiration (Paris)", DataPropertyName = nameof(MemoryItem.ExpiresAt), Width = 170 });
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(15, 18, 36), ForeColor = Color.White, SelectionBackColor = Color.FromArgb(58, 64, 110), SelectionForeColor = Color.White };
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 1 && e.Value is MemoryKind value) e.Value = KindName(value);
            if (e.ColumnIndex == 2 && e.Value is DateTimeOffset date)
            {
                e.Value = TimeZoneInfo.ConvertTime(date, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris")).ToString("g", CultureInfo.GetCultureInfo("fr-FR"));
                e.FormattingApplied = true;
            }
        };
        grid.SelectionChanged += (_, _) =>
        {
            if (Selected is MemoryItem item) { title.Text = item.Title; content.Text = item.Content; kind.SelectedIndex = (int)item.Kind; }
        };
        layout.Controls.Add(grid, 0, 3);
        var editorTitle = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        editorTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); editorTitle.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        kind.Items.AddRange(new object[] { "Souvenir durable", "Préférence", "Contexte temporaire" }); kind.SelectedIndex = 0;
        editorTitle.Controls.Add(title, 0, 0); editorTitle.Controls.Add(kind, 1, 0); layout.Controls.Add(editorTitle, 0, 4);
        layout.Controls.Add(content, 0, 5);
        var mutations = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        mutations.Controls.AddRange(new Control[] { Button("AddMemory", "Ajouter ce souvenir", AddAsync), Button("UpdateMemory", "Corriger la sélection", UpdateAsync),
            Button("DeleteMemory", "Supprimer la sélection", DeleteAsync), Button("ClearMemory", "Effacer tous les souvenirs", token => ExecuteAsync("clear", new(), token)) });
        layout.Controls.Add(mutations, 0, 6);
        var notes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        notes.Controls.Add(new Label { Text = "Suppression dans la base active : aucun effacement physique garanti. Exports/sauvegardes à gérer séparément.", AutoSize = true });
        notes.Controls.Add(status); layout.Controls.Add(notes, 0, 7);
        Controls.Add(layout);
        enabled.CheckedChanged += (_, _) => { if (!enabled.Checked) share.Checked = false; share.Enabled = enabled.Checked && !busy; };
        Shown += async (_, _) => await RunAsync(ReloadAsync);
        FormClosing += (_, _) => lifetime.Cancel();
        FormClosed += (_, _) => lifetime.Dispose();
    }
    private static string KindName(MemoryKind value) => value switch { MemoryKind.LongTerm => "Durable", MemoryKind.Preference => "Préférence", _ => "Contexte" };
    private Button Button(string name, string text, Func<CancellationToken, Task> action)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true };
        button.Click += async (_, _) => await RunAsync(action); buttons.Add(button); return button;
    }
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true; SetControls();
        try { await action(lifetime.Token); }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Opération interrompue."; }
        catch (Exception error)
        {
            if (!IsDisposed) status.Text = error is ArgumentException or InvalidOperationException or System.IO.InvalidDataException
                ? error.Message : "Stockage ou transfert mémoire indisponible. Les fichiers existants ne sont pas remplacés.";
        }
        finally { busy = false; if (!IsDisposed) SetControls(); }
    }
    private void SetControls()
    {
        bool available = !busy && !lifetime.IsCancellationRequested;
        foreach (Button button in buttons) button.Enabled = available && (button.Name is not ("AddMemory" or "UpdateMemory" or "Import" or "NewMemory") || active);
        enabled.Enabled = retention.Enabled = available;
        share.Enabled = available && enabled.Checked;
        title.Enabled = content.Enabled = kind.Enabled = available && active;
        search.Enabled = grid.Enabled = available;
    }
    private async Task ReloadAsync(CancellationToken token)
    {
        MemoryPolicy value = await memory.GetPolicyAsync(token);
        IReadOnlyList<MemoryItem> items = await memory.ListAsync(search.Text, token);
        if (IsDisposed) return;
        enabled.Checked = active = value.Enabled; share.Checked = value.ShareWithModel; retention.Value = value.RetentionDays;
        grid.DataSource = items.ToList(); status.Text = items.Count + " souvenir(s) · " + (value.ShareWithModel ? "utilisation locale par l’IA autorisée" : "non transmis au modèle");
        if (items.Count == 0) { title.Clear(); content.Clear(); }
    }
    private async Task ExecuteAsync(string operation, Dictionary<string, string> parameters, CancellationToken token)
    {
        var request = new ActionRequest(Guid.NewGuid(), Guid.NewGuid(), "memory." + operation, parameters, RequestOrigin.UserInterface, "memory/local", DateTimeOffset.UtcNow.AddSeconds(90));
        ActionResult result = await policy.ExecuteAsync(request, token);
        if (IsDisposed) return;
        if (result.Succeeded) { changed(); await ReloadAsync(token); }
        else status.Text = result.Message;
    }
    private Dictionary<string, string> EditParameters()
    {
        MemoryValidation.Content(title.Text.Trim(), content.Text.Trim(), (MemoryKind)kind.SelectedIndex);
        return new() { ["title"] = title.Text.Trim(), ["content"] = content.Text.Trim(), ["kind"] = kind.SelectedIndex.ToString(CultureInfo.InvariantCulture) };
    }
    private Task AddAsync(CancellationToken token) => ExecuteAsync("add", EditParameters(), token);
    private Task UpdateAsync(CancellationToken token)
    {
        MemoryItem item = Selected ?? throw new InvalidOperationException("Sélectionnez un souvenir à corriger.");
        Dictionary<string, string> parameters = EditParameters(); parameters["id"] = item.Id.ToString("D"); parameters["revision"] = item.Revision.ToString(CultureInfo.InvariantCulture);
        return ExecuteAsync("update", parameters, token);
    }
    private Task DeleteAsync(CancellationToken token)
    {
        MemoryItem item = Selected ?? throw new InvalidOperationException("Sélectionnez un souvenir à supprimer.");
        return ExecuteAsync("delete", new() { ["id"] = item.Id.ToString("D"), ["revision"] = item.Revision.ToString(CultureInfo.InvariantCulture), ["title"] = item.Title }, token);
    }
    private Task ApplyPolicyAsync(CancellationToken token) => ExecuteAsync("policy", new() { ["policy"] = JsonSerializer.Serialize(new MemoryPolicy(enabled.Checked, enabled.Checked && share.Checked, (int)retention.Value)) }, token);
    private async Task ExportAsync(CancellationToken token)
    {
        using var dialog = new SaveFileDialog { Title = "Exporter les souvenirs en JSON non chiffré — nouveau fichier uniquement", Filter = "Export IRIS (*.json)|*.json", FileName = "iris-souvenirs.json", OverwritePrompt = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await MemoryTransfer.WriteNewAsync(dialog.FileName, await memory.ExportAsync(token), token);
        if (!IsDisposed) status.Text = "Export terminé en clair. Aucun fichier existant remplacé.";
    }
    private async Task ImportAsync(CancellationToken token)
    {
        using var dialog = new OpenFileDialog { Title = "Choisir un export JSON IRIS (8 Mio maximum)", Filter = "Export IRIS (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string document = await MemoryTransfer.ReadAsync(dialog.FileName, token);
        int count = await memory.PreviewImportAsync(document, token);
        status.Text = count + " souvenir(s) valides ; confirmation requise.";
        await ExecuteAsync("import", new() { ["document"] = document }, token);
    }
}
