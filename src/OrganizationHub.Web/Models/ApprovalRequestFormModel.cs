using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestFormModel
{
    public string Title { get; set; } = string.Empty;
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
    public int StepCount { get; set; } = 2;
    public ApprovalStepCountSelection StepCountValue
    {
        get
        {
            return new ApprovalStepCountSelection(StepCount);
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
    }

    public ApprovalRequestCommand CreateCommand()
    {
        return new ApprovalRequestCommand(
            Title,
            SourceFolderIdValue,
            DestinationFolderIdValue,
            FileIdValue,
            StepCountValue,
            FirstApproverUserIdValue,
            SecondApproverUserIdValue,
            FinalApproverUserIdValue);
    }
}
