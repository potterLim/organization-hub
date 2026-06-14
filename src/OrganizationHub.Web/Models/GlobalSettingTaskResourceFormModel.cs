using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingTaskResourceFormModel
{
    public string OrganizationTaskListId { get; set; } = string.Empty;
    public TodoTaskListId OrganizationTaskListIdValue
    {
        get
        {
            return TodoTaskListId.CreateOptional(OrganizationTaskListId);
        }
    }
    public string ResponsibleTaskListId { get; set; } = string.Empty;
    public TodoTaskListId ResponsibleTaskListIdValue
    {
        get
        {
            return TodoTaskListId.CreateOptional(ResponsibleTaskListId);
        }
    }
}
