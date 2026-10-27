namespace OrganizationHub.Web.Models;

internal sealed class ApprovalRequestDraftState
{
    private readonly ApprovalRequestCommand mBaseline;

    public ApprovalRequestDraftState(ApprovalRequestFormModel form)
    {
        mBaseline = form.CreateCommand();
    }

    public bool HasChanges(ApprovalRequestFormModel form)
    {
        ApprovalRequestCommand current = form.CreateCommand();
        return current.Title != mBaseline.Title
            || current.SourceFolderId != mBaseline.SourceFolderId
            || current.DestinationFolderId != mBaseline.DestinationFolderId
            || current.FileId != mBaseline.FileId
            || current.StepCount != mBaseline.StepCount
            || current.FirstApproverUserId != mBaseline.FirstApproverUserId
            || current.SecondApproverUserId != mBaseline.SecondApproverUserId
            || current.FinalApproverUserId != mBaseline.FinalApproverUserId;
    }
}
