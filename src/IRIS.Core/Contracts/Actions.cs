using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.Core.Contracts;

public enum PermissionLevel { Denied, ReadOnly, Allowed, RequiresConfirmation }
public enum ActionRisk { Informational, LowRisk, Sensitive, Destructive, Prohibited }
public enum RequestOrigin { UserInterface, Model, ExternalContent }

public sealed class ActionRequest
{
    public Guid Id { get; }
    public Guid CorrelationId { get; }
    public string ActionName { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public RequestOrigin Origin { get; }
    public string Target { get; }
    public DateTimeOffset ExpiresAt { get; }

    public ActionRequest(Guid id, Guid correlationId, string actionName,
        IDictionary<string, string> parameters, RequestOrigin origin, string target, DateTimeOffset expiresAt)
    {
        Id = id;
        CorrelationId = correlationId;
        ActionName = actionName;
        Parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters, StringComparer.Ordinal));
        Origin = origin;
        Target = target;
        ExpiresAt = expiresAt;
    }
}

// Metadata comes exclusively from registered C# handlers, never from model output.
public sealed record ActionDescriptor(string Name, ActionRisk Risk, PermissionLevel Permission,
    string Target, string Effect, string Consequences, string? ApplicationIdentity = null);
public sealed record ActionResult(bool Succeeded, string Code, string Message);
public sealed record ConfirmationRequest(ActionRequest Request, ActionDescriptor Descriptor);
public sealed record ActionAuditEvent(Guid ActionId, Guid CorrelationId, string ActionName, string ResultCode, DateTimeOffset At);

public interface IActionHandler
{
    ActionDescriptor Descriptor { get; }
    bool Validate(ActionRequest request);
    Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
}

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(ConfirmationRequest request, CancellationToken cancellationToken);
}

public interface IActionAudit
{
    void Record(ActionAuditEvent entry);
}
