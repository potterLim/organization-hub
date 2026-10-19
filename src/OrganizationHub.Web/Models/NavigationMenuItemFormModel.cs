using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class NavigationMenuItemFormModel
{
    public int Revision
    {
        get; set;
    }
    public int Id
    {
        get; set;
    }
    public NavigationMenuItemId NavigationMenuItemIdValue
    {
        get
        {
            return NavigationMenuItemId.CreateOptional(Id);
        }
    }
    public int SectionId
    {
        get; set;
    }
    public NavigationMenuSectionId NavigationMenuSectionIdValue
    {
        get
        {
            return NavigationMenuSectionId.CreateOptional(SectionId);
        }
    }
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder
    {
        get; set;
    }
    public EMenuAccessLevel AccessLevel
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
    public bool IsVisibleToAllTeams
    {
        get; set;
    }
    public List<string> RequiredPermissionKeys { get; set; } = new List<string>();
    public IReadOnlyList<PermissionKey> RequiredPermissionKeyValues
    {
        get
        {
            return NavigationMenuPermissionKeys.CreatePermissionKeys(RequiredPermissionKeys);
        }
    }
    public List<int> TeamIds { get; set; } = new List<int>();
    public IReadOnlyList<OrganizationTeamId> TeamIdValues
    {
        get
        {
            List<OrganizationTeamId> teamIds = new List<OrganizationTeamId>();
            foreach (int teamId in TeamIds)
            {
                teamIds.Add(OrganizationTeamId.CreateExisting(teamId));
            }

            return teamIds;
        }
    }
}
