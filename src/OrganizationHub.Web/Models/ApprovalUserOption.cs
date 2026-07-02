namespace OrganizationHub.Web.Models;

public sealed class ApprovalUserOption
{
    public int UserId
    {
        get; set;
    }
    public string DisplayName { get; set; } = string.Empty;
    public int? PositionId
    {
        get; set;
    }
    public string PositionName { get; set; } = string.Empty;
    public int? TeamId
    {
        get; set;
    }
    public string TeamName { get; set; } = string.Empty;

    public string GetDisplayLabel()
    {
        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(TeamName, PositionName);
        string affiliationText = affiliationLabel.GetFullText();
        if (string.IsNullOrWhiteSpace(affiliationText))
        {
            return DisplayName;
        }

        return affiliationText;
    }

    public string GetLabel()
    {
        return GetDisplayLabel();
    }
}
