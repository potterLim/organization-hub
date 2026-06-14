using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingFileResourceFormModel
{
    public string NoticeFileId { get; set; } = string.Empty;
    public DriveItemId NoticeFileIdValue
    {
        get
        {
            return DriveItemId.CreateOptional(NoticeFileId);
        }
    }
    public string WorkGuidelineFileId { get; set; } = string.Empty;
    public DriveItemId WorkGuidelineFileIdValue
    {
        get
        {
            return DriveItemId.CreateOptional(WorkGuidelineFileId);
        }
    }
}
