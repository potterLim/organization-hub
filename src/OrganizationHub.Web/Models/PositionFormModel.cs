using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class PositionFormModel
{
    public int Id
    {
        get; set;
    }
    public int TeamId
    {
        get; set;
    }
    public OrganizationPositionId PositionIdValue
    {
        get
        {
            return OrganizationPositionId.CreateOptional(Id);
        }
    }
    public OrganizationTeamId TeamIdValue
    {
        get
        {
            return OrganizationTeamId.CreateOptional(TeamId);
        }
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
