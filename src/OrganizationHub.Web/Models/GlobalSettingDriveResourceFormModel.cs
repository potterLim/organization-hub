using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingDriveResourceFormModel
{
    public string OfficialRootFolderId { get; set; } = string.Empty;
    public DriveFolderId OfficialRootFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(OfficialRootFolderId);
        }
    }
    public string PublicFolderId { get; set; } = string.Empty;
    public DriveFolderId PublicFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(PublicFolderId);
        }
    }
    public string CommonFolderId { get; set; } = string.Empty;
    public DriveFolderId CommonFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(CommonFolderId);
        }
    }
    public string ResponsibleResourcesFolderId { get; set; } = string.Empty;
    public DriveFolderId ResponsibleResourcesFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(ResponsibleResourcesFolderId);
        }
    }
    public string WorkResourcesFolderId { get; set; } = string.Empty;
    public DriveFolderId WorkResourcesFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(WorkResourcesFolderId);
        }
    }
}
