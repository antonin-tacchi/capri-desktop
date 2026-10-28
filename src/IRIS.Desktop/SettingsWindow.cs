using System.Drawing;
using System.Windows.Forms;
using IRIS.Infrastructure;

namespace IRIS.Desktop;

internal sealed class SettingsWindow : Form
{
    public IrisSettings Settings { get; private set; }
    public SettingsWindow(IrisSettings settings)
    {
        Settings = settings;
        Text = "IRIS — Paramètres";
        ClientSize = new Size(490, 410);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(23, 26, 49);
        ForeColor = Color.White;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var onTop = new CheckBox { Name = "AlwaysOnTop", Text = "Toujours au premier plan", AutoSize = true, Checked = settings.AlwaysOnTop };
        var visible = new CheckBox { Name = "CharacterVisible", Text = "Afficher le personnage IRIS sur le bureau", AutoSize = true, Checked = settings.CharacterVisible };
        var scale = new NumericUpDown { Name = "CharacterScale", Minimum = 50, Maximum = 200, Value = settings.CharacterScale };
        var key = new NumericUpDown { Name = "EmergencyFunctionKey", Minimum = 6, Maximum = 12, Value = settings.EmergencyFunctionKey };
        var humor = new NumericUpDown { Name = "Humor", Minimum = 0, Maximum = 100, Value = settings.Humor };
        var save = new Button { Name = "Save", Text = "Enregistrer", AutoSize = true };
        panel.Controls.AddRange(new Control[] { onTop, visible, new Label { Text = "Échelle (%)", AutoSize = true }, scale,
            new Label { Text = "Arrêt : Ctrl+Alt+F… (6 à 12)", AutoSize = true }, key,
            new Label { Text = "Humour (0 : sobre, 100 : sarcasme doux)", AutoSize = true }, humor,
            new Label { Text = "Voix et animations : fenêtres dédiées.\nCapture écran et contrôle générique du PC indisponibles.", AutoSize = true }, save });
        save.Click += (_, _) =>
        {
            Settings = settings with { AlwaysOnTop = onTop.Checked, CharacterVisible = visible.Checked,
                CharacterScale = (int)scale.Value, EmergencyFunctionKey = (int)key.Value, Humor = (int)humor.Value };
            DialogResult = DialogResult.OK;
        };
        AcceptButton = save;
        Controls.Add(panel);
    }
}
