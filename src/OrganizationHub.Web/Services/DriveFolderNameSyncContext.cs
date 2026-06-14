using System.Collections.Generic;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class DriveFolderNameSyncContext
{
    public GoogleDriveFolderNameSyncSummary Summary
    {
        get; private init;
    }

    public List<string> FailureMessages
    {
        get; private init;
    }

    public HashSet<string> AppliedFolderIds
    {
        get; private init;
    }

    public DriveFolderNameSyncContext(
        GoogleDriveFolderNameSyncSummary summary,
        List<string> failureMessages,
        HashSet<string> appliedFolderIds)
    {
        Summary = summary;
        FailureMessages = failureMessages;
        AppliedFolderIds = appliedFolderIds;
    }
}
