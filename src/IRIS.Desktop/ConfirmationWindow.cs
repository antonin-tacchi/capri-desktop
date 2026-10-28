using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using IRIS.Core.Contracts;

namespace IRIS.Desktop;

internal sealed class ConfirmationWindow : Form
{
    public bool Approved { get; private set; }

    public ConfirmationWindow(ConfirmationRequest confirmation, Action emergencyStop)
    {
        Text = "IRIS — Autorisation ponctuelle";
        ClientSize = new Size(640, 430);
        MinimumSize = new Size(620, 400);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.FromArgb(23, 26, 49);
        ForeColor = Color.White;
        ActionDescriptor d = confirmation.Descriptor;
        ActionRequest r = confirmation.Request;
        var details = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            BackColor = BackColor, ForeColor = ForeColor, BorderStyle = BorderStyle.None,
            Text = $"Action : {d.Name}\r\nEffet : {d.Effect}\r\nApplication : {d.ApplicationIdentity ?? "IRIS"}\r\nRessource : {r.Target}\r\nParamètres : {JsonSerializer.Serialize(r.Parameters)}\r\nConséquences : {d.Consequences}\r\nExpiration UTC : {r.ExpiresAt:O}\r\nOpération : {r.Id}"
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(12), WrapContents = false };
        var refuse = new Button { Name = "Refuse", Text = "Refuser", AutoSize = true };
        var approve = new Button { Name = "Approve", Text = "Autoriser cette opération", AutoSize = true };
        var stop = new Button { Name = "EmergencyStop", Text = "ARRÊT D’URGENCE", AutoSize = true, BackColor = Color.DarkRed, ForeColor = Color.White };
        refuse.Click += (_, _) => Close();
        approve.Click += (_, _) => { Approved = true; Close(); };
        stop.Click += (_, _) => { emergencyStop(); Close(); };
        buttons.Controls.AddRange(new Control[] { refuse, approve, stop });
        Controls.Add(details);
        Controls.Add(buttons);
        Padding = new Padding(20);
        AcceptButton = refuse;
        CancelButton = refuse;
        Shown += (_, _) => refuse.Focus();
    }
}
