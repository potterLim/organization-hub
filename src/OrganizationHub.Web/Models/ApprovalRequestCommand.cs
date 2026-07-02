using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestCommand
{
    public ApprovalRequestTitle Title
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

    public ApprovalApproverSelection ApproverSelection
    {
        get;
    }

    public ApprovalUserId FirstApproverUserId
    {
        get
        {
            return ApproverSelection.FirstApproverUserId;
        }
    }

    public ApprovalUserId SecondApproverUserId
    {
        get
        {
            return ApproverSelection.SecondApproverUserId;
        }
    }

    public ApprovalUserId FinalApproverUserId
    {
        get
        {
            return ApproverSelection.FinalApproverUserId;
        }
    }

    public ApprovalRequestCommand(
        ApprovalRequestTitle title,
        DriveFolderId sourceFolderId,
        DriveFolderId destinationFolderId,
        DriveItemId fileId,
        ApprovalStepCountSelection stepCount,
        ApprovalApproverSelection approverSelection)
    {
        Title = title;
        SourceFolderId = sourceFolderId;
        DestinationFolderId = destinationFolderId;
        FileId = fileId;
        StepCount = stepCount;
        if (approverSelection == null)
        {
            throw new ArgumentNullException(nameof(approverSelection));
        }

        ApproverSelection = approverSelection;
    }
}
