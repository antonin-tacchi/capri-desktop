using System;
using IRIS.Core.Contracts;

namespace IRIS.Security.Permissions;

public sealed class PermissionService
{
    public PermissionLevel Evaluate(ActionDescriptor descriptor)
    {
        if (!Enum.IsDefined(descriptor.Risk) || !Enum.IsDefined(descriptor.Permission)
            || descriptor.Risk == ActionRisk.Prohibited || descriptor.Permission == PermissionLevel.Denied)
            return PermissionLevel.Denied;
        if (descriptor.Risk is ActionRisk.Sensitive or ActionRisk.Destructive)
            return PermissionLevel.RequiresConfirmation;
        if (descriptor.Permission == PermissionLevel.ReadOnly && descriptor.Risk != ActionRisk.Informational)
            return PermissionLevel.Denied;
        return descriptor.Permission;
    }
}
