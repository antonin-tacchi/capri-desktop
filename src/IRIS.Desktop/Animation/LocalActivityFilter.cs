using System;
using System.Windows.Forms;

namespace IRIS.Desktop.Animation;

// Application message queue only: no global hook, key value or foreign window.
internal sealed class LocalActivityFilter(Action activity) : IMessageFilter
{
    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg is 0x0100 or 0x0201 or 0x0204 && Control.FromHandle(message.HWnd) != null) activity();
        return false;
    }
}
