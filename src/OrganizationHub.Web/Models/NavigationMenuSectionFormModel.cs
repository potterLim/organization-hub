using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class NavigationMenuSectionFormModel
{
    public int Revision { get; set; }
    public int Id
    {
        get; set;
    }

    public NavigationMenuSectionId NavigationMenuSectionIdValue
    {
        get
        {
            return NavigationMenuSectionId.CreateOptional(Id);
        }
    }

    public string DisplayName { get; set; } = string.Empty;
}
