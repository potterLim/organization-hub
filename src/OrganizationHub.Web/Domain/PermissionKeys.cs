namespace OrganizationHub.Web.Domain;

public static class PermissionKeys
{
    public const string SYSTEM_SETTINGS_MANAGE = "system.settings.manage";
    public const string ORGANIZATION_MANAGE = "organization.manage";
    public const string POSITIONS_MANAGE = "positions.manage";
    public const string PERMISSIONS_MANAGE = "permissions.manage";
    public const string USERS_MANAGE = "users.manage";
    public const string GOOGLE_CONNECTION_MANAGE = "google.connection.manage";
    public const string MENUS_MANAGE = "menus.manage";

    public const string MENU_READ = "menu.read";

    public const string FILES_READ = "files.read";
    public const string FILES_UPLOAD = "files.upload";
    public const string FILES_UPDATE = "files.update";
    public const string FILES_DELETE = "files.delete";
    public const string FILES_APPROVED_STORE = "files.approved.store";

    public const string CALENDAR_READ = "calendar.read";
    public const string CALENDAR_CREATE = "calendar.create";
    public const string CALENDAR_UPDATE = "calendar.update";
    public const string CALENDAR_DELETE = "calendar.delete";
    public const string CALENDAR_AUDIT_READ = "calendar.audit.read";

    public const string TODOS_READ = "todos.read";
    public const string TODOS_CREATE = "todos.create";
    public const string TODOS_UPDATE = "todos.update";
    public const string TODOS_DELETE = "todos.delete";

    public const string APPROVAL_REQUEST = "approval.request";
    public const string APPROVAL_APPROVE = "approval.approve";
    public const string APPROVAL_FINAL_APPROVE = "approval.finalApprove";
    public const string APPROVAL_HISTORY_READ = "approval.history.read";
    public const string APPROVAL_WORKFLOWS_MANAGE = "approval.workflows.manage";
}
