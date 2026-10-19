using System;

namespace OrganizationHub.Web.Domain;

public static class ResourceOperationPermission
{
    public static PermissionKey GetCalendarPermission(EResourceOperation operation)
    {
        switch (operation)
        {
            case EResourceOperation.Create:
                return new PermissionKey(PermissionKeys.CALENDAR_CREATE);
            case EResourceOperation.Update:
                return new PermissionKey(PermissionKeys.CALENDAR_UPDATE);
            case EResourceOperation.Delete:
                return new PermissionKey(PermissionKeys.CALENDAR_DELETE);
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public static PermissionKey GetTodoPermission(EResourceOperation operation)
    {
        switch (operation)
        {
            case EResourceOperation.Create:
                return new PermissionKey(PermissionKeys.TODOS_CREATE);
            case EResourceOperation.Update:
                return new PermissionKey(PermissionKeys.TODOS_UPDATE);
            case EResourceOperation.Delete:
                return new PermissionKey(PermissionKeys.TODOS_DELETE);
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
