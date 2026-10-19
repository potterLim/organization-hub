namespace OrganizationHub.Web.Models;

internal sealed class DashboardAccess
{
    public required bool CanOpenInbox
    {
        get; init;
    }
    public required bool CanOpenHistory
    {
        get; init;
    }
    public required bool CanOpenTodos
    {
        get; init;
    }
    public required bool CanOpenCalendar
    {
        get; init;
    }
    public required bool CanOpenMaterials
    {
        get; init;
    }
}
