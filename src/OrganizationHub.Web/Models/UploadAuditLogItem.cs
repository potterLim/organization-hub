using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UploadAuditLogItem
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; set;
    }
    public string UploaderDisplayName { get; set; } = string.Empty;
    public string UploaderTeamName { get; set; } = string.Empty;
    public string UploaderPositionName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public DateTime UploadedAt
    {
        get; set;
    }

    public string GetUploaderAffiliationText()
    {
        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(UploaderTeamName, UploaderPositionName);
        string affiliationText = affiliationLabel.GetFullText();
        if (string.IsNullOrWhiteSpace(affiliationText))
        {
            return UploaderDisplayName;
        }

        return affiliationText;
    }

    public string GetUploaderPositionText()
    {
        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(UploaderTeamName, UploaderPositionName);
        string positionText = affiliationLabel.GetPositionText();
        if (string.IsNullOrWhiteSpace(positionText))
        {
            return UploaderDisplayName;
        }

        return positionText;
    }
}
