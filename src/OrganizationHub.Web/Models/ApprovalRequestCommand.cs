using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestCommand
{
    public string Title
    {
        get;
    }

    public DriveFolderId SourceFolderId
    {
        get;
    }

    public DriveFolderId DestinationFolderId
    {
        get;
    }

    public DriveItemId FileId
    {
        get;
    }

    public ApprovalStepCountSelection StepCount
    {
        get;
    }

    public ApprovalUserId FirstApproverUserId
    {
        get;
    }

    public ApprovalUserId SecondApproverUserId
    {
        get;
    }

    public ApprovalUserId FinalApproverUserId
    {
        get;
    }

    public ApprovalRequestCommand(
        string title,
        DriveFolderId sourceFolderId,
        DriveFolderId destinationFolderId,
        DriveItemId fileId,
        ApprovalStepCountSelection stepCount,
        ApprovalUserId firstApproverUserId,
        ApprovalUserId secondApproverUserId,
        ApprovalUserId finalApproverUserId)
    {
        Title = title;
        SourceFolderId = sourceFolderId;
        DestinationFolderId = destinationFolderId;
        FileId = fileId;
        StepCount = stepCount;
        FirstApproverUserId = firstApproverUserId;
        SecondApproverUserId = secondApproverUserId;
        FinalApproverUserId = finalApproverUserId;
    }
}
