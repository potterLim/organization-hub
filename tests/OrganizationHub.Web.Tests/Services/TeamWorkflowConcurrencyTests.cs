using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TeamWorkflowConcurrencyTests
{
    [Fact]
    public async Task StaleTeamFormCannotRestoreDeactivatedTeamAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        {
            TeamManagementService teams = new TeamManagementService(factory);
            Team team = Assert.Single(await teams.GetTeamsAsync(CancellationToken.None));
            TeamFormModel form = new TeamFormModel();
            form.Id = team.Id;
            form.TeamKey = team.TeamKey;
            form.DisplayName = "오래된 화면의 이름 수정";
            Assert.True((await teams.DeactivateTeamAsync(OrganizationTeamId.CreateExisting(team.Id), CancellationToken.None)).IsSuccess);
            ServiceResult result = await teams.UpdateTeamAsync(form, CancellationToken.None);
            Assert.True(result.IsConcurrencyConflict);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.False(db.Teams.Single().IsActive);
                Assert.Equal(team.CalendarId, db.Teams.Single().CalendarId);
            }
        }
    }

    [Fact]
    public async Task StaleWorkflowFormCannotRestoreDeactivatedWorkflowAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        {
            ApprovalWorkflowManagementService service = new ApprovalWorkflowManagementService(factory);
            ApprovalWorkflowFormModel form = createWorkflowForm();
            Assert.True((await service.CreateWorkflowAsync(form, CancellationToken.None)).IsSuccess);
            form.Id = Assert.Single(await service.GetWorkflowsAsync(CancellationToken.None)).Id;
            Assert.True((await service.DeactivateWorkflowAsync(ApprovalWorkflowId.CreateOptional(form.Id), CancellationToken.None)).IsSuccess);
            form.Description = "오래된 화면에서 설명만 수정";
            ServiceResult result = await service.UpdateWorkflowAsync(form, CancellationToken.None);
            Assert.True(result.IsConcurrencyConflict);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.False(db.ApprovalWorkflows.Single().IsActive);
                Assert.Empty(db.ApprovalWorkflows.Single().Description);
                Assert.Single(db.ApprovalWorkflowSteps);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDatabaseWritersRejectLostUpdatesAsync(bool isWorkflow)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        {
            if (isWorkflow)
            {
                Assert.True((await new ApprovalWorkflowManagementService(factory).CreateWorkflowAsync(createWorkflowForm(), CancellationToken.None)).IsSuccess);
            }
            using (OrganizationHubDbContext first = factory.CreateDbContext())
            using (OrganizationHubDbContext second = factory.CreateDbContext())
            {
                if (isWorkflow)
                {
                    ApprovalWorkflow a = first.ApprovalWorkflows.Single();
                    ApprovalWorkflow b = second.ApprovalWorkflows.Single();
                    a.IsActive = false;
                    b.Name = "충돌하는 수정";
                }
                else
                {
                    Team a = first.Teams.Single();
                    Team b = second.Teams.Single();
                    a.IsActive = false;
                    b.DisplayName = "충돌하는 수정";
                }
                await first.SaveChangesAsync();
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }
        }
    }

    private static ApprovalWorkflowFormModel createWorkflowForm()
    {
        ApprovalWorkflowFormModel form = new ApprovalWorkflowFormModel();
        form.Name = "검증 경로";
        ApprovalWorkflowStepFormModel step = new ApprovalWorkflowStepFormModel();
        step.StepOrder = 1;
        step.StepName = "최종 승인";
        step.ApproverSelectionType = EApproverSelectionType.SpecificUser;
        step.ApproverUserId = 1;
        step.IsRequired = true;
        form.Steps.Add(step);
        return form;
    }
}
