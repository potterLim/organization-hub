using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class PositionFormModel
{
    public int ID
    {
        get; set;
    }
    public int TeamID
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder
    {
        get; set;
    }
    public bool IsActive { get; set; } = true;
    public bool IsSystemAdministrator
    {
        get; set;
    }
    public List<PositionPermissionGrantFormModel> PermissionGrants { get; set; } = new List<PositionPermissionGrantFormModel>();
}
