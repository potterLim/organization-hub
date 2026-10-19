using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class SettingFileAssignmentCommand
{
    public required ESettingFileAssignmentTarget Target
    {
        get; init;
    }
    public required DriveItemId FileId
    {
        get; init;
    }
    public required int ExpectedRevision
    {
        get; init;
    }
}
