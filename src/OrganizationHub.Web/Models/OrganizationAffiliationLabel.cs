namespace OrganizationHub.Web.Models;

public readonly record struct OrganizationAffiliationLabel
{
    public string TeamName
    {
        get;
    }

    public string PositionName
    {
        get;
    }

    public OrganizationAffiliationLabel(string teamName, string positionName)
    {
        TeamName = normalizeText(teamName);
        PositionName = normalizeText(positionName);
    }

    public string GetFullText()
    {
        if (string.IsNullOrWhiteSpace(TeamName))
        {
            return PositionName;
        }

        if (string.IsNullOrWhiteSpace(PositionName))
        {
            return TeamName;
        }

        return TeamName + " " + PositionName;
    }

    public string GetPositionText()
    {
        return PositionName;
    }

    private static string normalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim();
    }
}
