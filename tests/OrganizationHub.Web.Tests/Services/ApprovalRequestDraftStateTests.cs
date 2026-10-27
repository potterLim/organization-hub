using System;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ApprovalRequestDraftStateTests
{
    [Fact]
    public void EveryRequestSelectionIsProtectedAndRestoringDefaultsClearsChanges()
    {
        Action<ApprovalRequestFormModel>[] changes = new Action<ApprovalRequestFormModel>[]
        {
            form => form.Title = "결재 제목",
            form => form.SourceFolderId = "source",
            form => form.DestinationFolderId = "destination",
            form => form.FileId = "file",
            form => form.StepCount = form.StepCount == 1 ? 2 : 1,
            form => form.FirstApproverUserId = 1,
            form => form.SecondApproverUserId = 2,
            form => form.FinalApproverUserId = 3
        };
        foreach (Action<ApprovalRequestFormModel> change in changes)
        {
            ApprovalRequestFormModel form = new ApprovalRequestFormModel();
            ApprovalRequestDraftState state = new ApprovalRequestDraftState(form);
            Assert.False(state.HasChanges(form));
            change(form);
            Assert.True(state.HasChanges(form));
            Assert.False(state.HasChanges(new ApprovalRequestFormModel()));
        }
    }

    [Fact]
    public void ConfiguredDefaultsAndSuccessfulSubmissionBecomeCleanBaselines()
    {
        ApprovalRequestFormModel form = new ApprovalRequestFormModel();
        form.SourceFolderId = "team";
        form.DestinationFolderId = "approved";
        form.FinalApproverUserId = 7;
        ApprovalRequestDraftState state = new ApprovalRequestDraftState(form);
        Assert.False(state.HasChanges(form));
        form.FileId = "selected";
        Assert.True(state.HasChanges(form));
        state = new ApprovalRequestDraftState(form);
        Assert.False(state.HasChanges(form));
    }
}
