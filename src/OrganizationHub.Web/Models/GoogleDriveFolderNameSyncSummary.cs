namespace OrganizationHub.Web.Models;

public sealed class GoogleDriveFolderNameSyncSummary
{
    public int TargetCount
    {
        get; set;
    }
    public int RenamedCount
    {
        get; set;
    }
    public int MovedCount
    {
        get; set;
    }
    public int CreatedCount
    {
        get; set;
    }
    public int AlreadyStandardCount
    {
        get; set;
    }
    public int SkippedCount
    {
        get; set;
    }
}
