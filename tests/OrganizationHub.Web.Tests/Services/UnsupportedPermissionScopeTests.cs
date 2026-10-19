using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class UnsupportedPermissionScopeTests
{
    [Fact]
    public async Task PositionCannotSaveAnUnsupportedDescendantScopeAsync()
    {
        using (TestDbContextFactory factory = AdministrationConcurrencyRegressionTests.createFactory())
        {
            PositionFormModel form = new PositionFormModel();
            form.TeamId = 1;
            form.Name = "하위 팀이 없는 범위";
            form.IsActive = true;
            PositionPermissionGrantFormModel grant = new PositionPermissionGrantFormModel();
            grant.PermissionKey = PermissionKeys.CALENDAR_READ;
            grant.IsAllowed = true;
            grant.ScopeType = EPermissionScopeType.OwnTeamAndChildren;
            form.PermissionGrants.Add(grant);
            ServiceResult result = await new PositionManagementService(factory).CreatePositionAsync(form, CancellationToken.None);
            Assert.False(result.IsSuccess);
        }
    }

    [Fact]
    public async Task WorkflowCannotSaveAnUnsupportedDescendantScopeAsync()
    {
        using (TestDbContextFactory factory = AdministrationConcurrencyRegressionTests.createFactory())
        {
            ApprovalWorkflowFormModel form = new ApprovalWorkflowFormModel();
            form.Name = "하위 팀이 없는 결재 범위";
            ApprovalWorkflowStepFormModel step = new ApprovalWorkflowStepFormModel();
            step.StepOrder = 1;
            step.StepName = "확인";
            step.ApproverSelectionType = EApproverSelectionType.SpecificUser;
            step.ApproverUserId = 1;
            step.ScopeType = EPermissionScopeType.OwnTeamAndChildren;
            step.IsRequired = true;
            form.Steps.Add(step);
            ServiceResult result = await new ApprovalWorkflowManagementService(factory).CreateWorkflowAsync(form, CancellationToken.None);
            Assert.False(result.IsSuccess);
        }
    }
}
