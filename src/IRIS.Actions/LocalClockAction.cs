using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;

namespace IRIS.Actions;

public sealed class LocalClockAction : IActionHandler
{
    private readonly bool date;
    private readonly TimeProvider time;
    public ActionDescriptor Descriptor { get; }
    public LocalClockAction(bool date, TimeProvider? time = null)
    {
        this.date = date;
        this.time = time ?? TimeProvider.System;
        Descriptor = new(date ? "clock.date" : "clock.time", ActionRisk.Informational,
            PermissionLevel.ReadOnly, "clock/Europe-Paris", "Lire l’horloge en Europe/Paris.", "Aucune modification.");
    }
    public bool Validate(ActionRequest request) => request.Parameters.Count == 0;
    public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
        return Task.FromResult(new ActionResult(true, "completed", now.ToString(date ? "D" : "HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR"))));
    }
}
