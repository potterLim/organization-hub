using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ApprovalWorkflowManagementServiceTests
{
    [Fact]
    public async Task CreateWorkflowStoresApprovalStepsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedApprovalDataAsync(dbContextFactory);
            ApprovalWorkflowManagementService workflowManagementService = new ApprovalWorkflowManagementService(dbContextFactory);

            ApprovalWorkflowFormModel formModel = new ApprovalWorkflowFormModel();
            formModel.Name = "기본 결재 경로";
            formModel.Priority = 10;
            formModel.IsActive = true;
            ApprovalWorkflowStepFormModel stepFormModel = new ApprovalWorkflowStepFormModel();
            stepFormModel.StepOrder = 1;
            stepFormModel.StepName = "팀 검토";
            stepFormModel.ApproverSelectionType = EApproverSelectionType.PositionInRequesterTeam;
            stepFormModel.ApproverPositionId = 1;
            stepFormModel.IsRequired = true;
            formModel.Steps.Add(stepFormModel);

            ServiceResult result = await workflowManagementService.CreateWorkflowAsync(formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                ApprovalWorkflow workflow = dbContext.ApprovalWorkflows.Single();
                ApprovalWorkflowStep step = dbContext.ApprovalWorkflowSteps.Single();
                Assert.Equal("기본 결재 경로", workflow.Name);
                Assert.Equal("팀 검토", step.StepName);
                Assert.Equal(EApproverSelectionType.PositionInRequesterTeam, step.ApproverSelectionType);
            }
        }
    }

    [Fact]
    public async Task PermissionHolderStepRequiresPermissionKeyAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedApprovalDataAsync(dbContextFactory);
            ApprovalWorkflowManagementService workflowManagementService = new ApprovalWorkflowManagementService(dbContextFactory);

            ApprovalWorkflowFormModel formModel = new ApprovalWorkflowFormModel();
            formModel.Name = "권한 결재 경로";
            ApprovalWorkflowStepFormModel stepFormModel = new ApprovalWorkflowStepFormModel();
            stepFormModel.StepOrder = 1;
            stepFormModel.StepName = "권한 승인";
            stepFormModel.ApproverSelectionType = EApproverSelectionType.PermissionHolder;
            stepFormModel.IsRequired = true;
            formModel.Steps.Add(stepFormModel);

            ServiceResult result = await workflowManagementService.CreateWorkflowAsync(formModel, CancellationToken.None);

            Assert.False(result.IsSuccess);
        }
    }

    private static async Task seedApprovalDataAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 1;
            team.TeamKey = "Documents";
            team.DisplayName = "문서팀";
            team.IsActive = true;
            dbContext.Teams.Add(team);

            OrganizationPosition position = new OrganizationPosition();
            position.Id = 1;
            position.Team = team;
            position.Name = "문서 리드";
            position.IsActive = true;
            dbContext.Positions.Add(position);

            UserAccount userAccount = new UserAccount();
            userAccount.Id = 1;
            userAccount.Username = "lead";
            userAccount.DisplayName = "문서 리드";
            userAccount.PasswordHash = "hash";
            userAccount.Role = EUserRole.TeamLeader;
            userAccount.Team = team;
            userAccount.Position = position;
            userAccount.IsActive = true;
            dbContext.UserAccounts.Add(userAccount);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
