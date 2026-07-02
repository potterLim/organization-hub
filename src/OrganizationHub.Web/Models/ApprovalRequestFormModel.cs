using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestFormModel
{
    public string Title { get; set; } = string.Empty;
    public ApprovalRequestTitle TitleValue
    {
        get
        {
            return ApprovalRequestTitle.CreateOptional(Title);
        }
    }
    public string SourceFolderId { get; set; } = string.Empty;
    public DriveFolderId SourceFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(SourceFolderId);
        }
    }
    public string DestinationFolderId { get; set; } = string.Empty;
    public DriveFolderId DestinationFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(DestinationFolderId);
        }
    }
    public string FileId { get; set; } = string.Empty;
    public DriveItemId FileIdValue
    {
        get
        {
            return DriveItemId.CreateOptional(FileId);
        }
    }
    public int StepCount { get; set; } = ApprovalStepCount.DEFAULT_VALUE;
    public ApprovalStepCountSelection StepCountValue
    {
        get
        {
            return new ApprovalStepCountSelection(StepCount);
        }
        set
        {
            StepCount = value.GetRequiredValue();
        }
    }

    public int FirstApproverUserId
    {
        get; set;
    }
    public ApprovalUserId FirstApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(FirstApproverUserId);
        }
        set
        {
            FirstApproverUserId = value.GetOptionalValue();
        }
    }
    public int SecondApproverUserId
    {
        get; set;
    }
    public ApprovalUserId SecondApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(SecondApproverUserId);
        }
        set
        {
            SecondApproverUserId = value.GetOptionalValue();
        }
    }
    public int FinalApproverUserId
    {
        get; set;
    }
    public ApprovalUserId FinalApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(FinalApproverUserId);
        }
        set
        {
            FinalApproverUserId = value.GetOptionalValue();
        }
    }

    public void ClearFirstApprover()
    {
        FirstApproverUserIdValue = ApprovalUserId.Empty;
    }

    public void ClearSecondApprover()
    {
        SecondApproverUserIdValue = ApprovalUserId.Empty;
    }

    public void ClearFinalApprover()
    {
        FinalApproverUserIdValue = ApprovalUserId.Empty;
    }

    public ApprovalRequestCommand CreateCommand()
    {
        return CreateCommand(TitleValue);
    }

    public ApprovalRequestCommand CreateCommand(ApprovalRequestTitle title)
    {
        return new ApprovalRequestCommand(
            title,
            SourceFolderIdValue,
            DestinationFolderIdValue,
            FileIdValue,
            StepCountValue,
            new ApprovalApproverSelection(
                FirstApproverUserIdValue,
                SecondApproverUserIdValue,
                FinalApproverUserIdValue));
    }
}
