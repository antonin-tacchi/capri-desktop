using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IRIS.Core.Contracts;

namespace IRIS.Desktop;

internal sealed class WindowsConfirmationService(Form owner, Action emergencyStop,
    Func<ConfirmationRequest, Action, ConfirmationWindow>? createWindow = null) : IConfirmationService
{
    public async Task<bool> ConfirmAsync(ConfirmationRequest request, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmationWindow? dialog = null;
        void Dispatch(Action action)
        {
            try { owner.BeginInvoke(action); }
            catch (InvalidOperationException) { completion.TrySetResult(false); }
        }
        using var registration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
            Dispatch(() => { if (dialog is { IsDisposed: false }) dialog.Close(); });
        });
        Dispatch(() =>
        {
            if (cancellationToken.IsCancellationRequested || owner.IsDisposed)
            {
                completion.TrySetResult(false);
                return;
            }
            try
            {
                dialog = createWindow == null ? new ConfirmationWindow(request, emergencyStop) : createWindow(request, emergencyStop);
                dialog.TopMost = owner.TopMost;
                dialog.FormClosed += (_, _) =>
                {
                    completion.TrySetResult(dialog.Approved && !cancellationToken.IsCancellationRequested);
                    dialog.Dispose();
                };
                // Owned modeless form keeps the main emergency button accessible.
                dialog.Show(owner);
            }
            catch (Exception)
            {
                completion.TrySetResult(false);
                dialog?.Dispose();
            }
        });
        return await completion.Task.ConfigureAwait(false);
    }
}
