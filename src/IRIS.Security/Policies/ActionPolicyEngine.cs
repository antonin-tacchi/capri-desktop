using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Core.Services;
using IRIS.Security.Permissions;

namespace IRIS.Security.Policies;

public sealed class ActionPolicyEngine
{
    private readonly Dictionary<string, IActionHandler> handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, byte> consumed = new();
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly PermissionService permissions;
    private readonly IConfirmationService confirmations;
    private readonly IActionAudit audit;
    private readonly EmergencyStopService emergency;
    private readonly TimeProvider time;

    public ActionPolicyEngine(IEnumerable<IActionHandler> registry, PermissionService permissions,
        IConfirmationService confirmations, IActionAudit audit, EmergencyStopService emergency, TimeProvider? time = null)
    {
        foreach (IActionHandler handler in registry) handlers.Add(handler.Descriptor.Name, handler);
        this.permissions = permissions;
        this.confirmations = confirmations;
        this.audit = audit;
        this.emergency = emergency;
        this.time = time ?? TimeProvider.System;
    }

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken = default)
    {
        TimeSpan remaining = request.ExpiresAt - time.GetUtcNow();
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(2)) return Finish(request, false, "invalid_request");
        using var deadline = new CancellationTokenSource(remaining, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, emergency.Token, deadline.Token);
        bool entered = false;
        try
        {
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            linked.Token.ThrowIfCancellationRequested();
            if (request.Id == Guid.Empty || request.CorrelationId == Guid.Empty
                || !Enum.IsDefined(request.Origin) || request.Origin == RequestOrigin.ExternalContent
                || string.IsNullOrWhiteSpace(request.ActionName) || string.IsNullOrWhiteSpace(request.Target)
                || request.ExpiresAt <= time.GetUtcNow() || request.ExpiresAt > time.GetUtcNow().AddMinutes(2))
                return Finish(request, false, "invalid_request");
            // Hard bound avoids unbounded replay tracking. Restart is required after this limit.
            if (consumed.Count >= 10000 || !consumed.TryAdd(request.Id, 0)) return Finish(request, false, "replay_or_capacity");
            if (!handlers.TryGetValue(request.ActionName, out IActionHandler? handler)) return Finish(request, false, "unknown_action");
            ActionDescriptor descriptor = handler.Descriptor;
            if (descriptor.Target != request.Target || !handler.Validate(request)) return Finish(request, false, "invalid_parameters");
            PermissionLevel permission = permissions.Evaluate(descriptor);
            if (permission == PermissionLevel.Denied) return Finish(request, false, "denied");
            if (permission == PermissionLevel.RequiresConfirmation)
            {
                // The response is scoped to this immutable request, in this invocation only.
                bool accepted = await confirmations.ConfirmAsync(new(request, descriptor), linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                if (!accepted) return Finish(request, false, "confirmation_refused");
            }
            linked.Token.ThrowIfCancellationRequested();
            if (request.ExpiresAt <= time.GetUtcNow()) return Finish(request, false, "expired");
            // Audit failure blocks execution. Entries contain no parameters, messages or secrets.
            audit.Record(new(request.Id, request.CorrelationId, descriptor.Name, "authorized", time.GetUtcNow()));
            linked.Token.ThrowIfCancellationRequested();
            ActionResult result = await handler.ExecuteAsync(request, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            return Finish(request, result.Succeeded, result.Succeeded ? "completed" : "handler_failed", result.Message);
        }
        catch (OperationCanceledException)
        {
            return Finish(request, false, deadline.IsCancellationRequested && !emergency.IsStopped && !cancellationToken.IsCancellationRequested ? "expired" : "cancelled");
        }
        catch (Exception) { return Finish(request, false, "internal_error"); }
        finally { if (entered) serial.Release(); }
    }

    private ActionResult Finish(ActionRequest request, bool success, string code, string? message = null)
    {
        try { audit.Record(new(request.Id, request.CorrelationId, request.ActionName != null && handlers.ContainsKey(request.ActionName) ? request.ActionName : "unregistered", code, time.GetUtcNow())); }
        catch (Exception) { return new(false, "audit_failed", "Journal indisponible ; opération bloquée ou résultat à vérifier."); }
        return new(success, code, message ?? "Opération refusée ou interrompue : " + code);
    }
}
