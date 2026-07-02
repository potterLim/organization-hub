using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    private static async Task<int> seedConfiguredApprovalWorkflowAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition requesterPosition = new OrganizationPosition();
            requesterPosition.TeamId = 1;
            requesterPosition.Name = "워크플로 요청자";
            requesterPosition.Description = string.Empty;
            requesterPosition.SortOrder = 10;
            requesterPosition.IsActive = true;
            addPermissionGrant(requesterPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(requesterPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.OwnTeam);

            OrganizationPosition leaderPosition = new OrganizationPosition();
            leaderPosition.TeamId = 1;
            leaderPosition.Name = "워크플로 팀 결재자";
            leaderPosition.Description = string.Empty;
            leaderPosition.SortOrder = 20;
            leaderPosition.IsActive = true;
            addPermissionGrant(leaderPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.OwnTeam);

            OrganizationPosition finalApproverPosition = new OrganizationPosition();
            finalApproverPosition.TeamId = 2;
            finalApproverPosition.Name = "워크플로 최종 결재자";
            finalApproverPosition.Description = string.Empty;
            finalApproverPosition.SortOrder = 10;
            finalApproverPosition.IsActive = true;
            addPermissionGrant(finalApproverPosition, PermissionKeys.APPROVAL_FINAL_APPROVE, EPermissionScopeType.Global);

            UserAccount requester = dbContext.UserAccounts.Single(user => user.Id == 1);
            UserAccount leader = dbContext.UserAccounts.Single(user => user.Id == 2);
            UserAccount finalApprover = dbContext.UserAccounts.Single(user => user.Id == 3);
            requester.Position = requesterPosition;
            leader.Position = leaderPosition;
            finalApprover.Position = finalApproverPosition;

            ApprovalWorkflow workflow = new ApprovalWorkflow();
            workflow.Name = "팀 자료 결재";
            workflow.Description = string.Empty;
            workflow.AppliesToTeamId = 1;
            workflow.Priority = 0;
            workflow.IsActive = true;
            ApprovalWorkflowStep firstStep = new ApprovalWorkflowStep();
            firstStep.StepOrder = 1;
            firstStep.StepName = "팀 확인";
            firstStep.ApproverSelectionType = EApproverSelectionType.PositionInRequesterTeam;
            firstStep.ApproverPosition = leaderPosition;
            firstStep.RequiredPermissionKey = PermissionKeys.APPROVAL_APPROVE;
            firstStep.ScopeType = EPermissionScopeType.OwnTeam;
            firstStep.IsRequired = true;
            ApprovalWorkflowStep finalStep = new ApprovalWorkflowStep();
            finalStep.StepOrder = 2;
            finalStep.StepName = "최종 승인";
            finalStep.ApproverSelectionType = EApproverSelectionType.SpecificUser;
            finalStep.ApproverUserId = 3;
            finalStep.RequiredPermissionKey = PermissionKeys.APPROVAL_FINAL_APPROVE;
            finalStep.ScopeType = EPermissionScopeType.Global;
            finalStep.IsRequired = true;
            workflow.Steps.Add(firstStep);
            workflow.Steps.Add(finalStep);
            dbContext.Positions.Add(requesterPosition);
            dbContext.Positions.Add(leaderPosition);
            dbContext.Positions.Add(finalApproverPosition);
            dbContext.ApprovalWorkflows.Add(workflow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return leaderPosition.Id;
        }
    }

    private static async Task seedRequesterPositionApprovalWorkflowAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            UserAccount requester = dbContext.UserAccounts.Single(user => user.Id == 1);
            int requesterPositionId = requester.PositionId.GetValueOrDefault();
            ApprovalWorkflow workflow = new ApprovalWorkflow();
            workflow.Name = "요청 직책별 결재";
            workflow.Description = string.Empty;
            workflow.AppliesToTeamId = 1;
            workflow.AppliesToPositionId = requesterPositionId;
            workflow.Priority = 100;
            workflow.IsActive = true;
            ApprovalWorkflowStep finalStep = new ApprovalWorkflowStep();
            finalStep.StepOrder = 1;
            finalStep.StepName = "최종 승인";
            finalStep.ApproverSelectionType = EApproverSelectionType.SpecificUser;
            finalStep.ApproverUserId = 3;
            finalStep.RequiredPermissionKey = PermissionKeys.APPROVAL_FINAL_APPROVE;
            finalStep.ScopeType = EPermissionScopeType.Global;
            finalStep.IsRequired = true;
            workflow.Steps.Add(finalStep);
            dbContext.ApprovalWorkflows.Add(workflow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static TestDbContextFactory createApprovalDbContextFactory(EFinalApproverSeedMode finalApproverSeedMode)
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.OfficialRootFolderId = "official-root";
            Team planningTeam = new Team();
            planningTeam.Id = 1;
            planningTeam.TeamKey = "Planning";
            planningTeam.DisplayName = "기획팀";
            planningTeam.IsActive = true;
            planningTeam.SortOrder = 10;
            planningTeam.MaterialsFolderId = "planning-materials-folder";
            planningTeam.ApprovedFolderId = "planning-approved-folder";
            Team managementTeam = new Team();
            managementTeam.Id = 2;
            managementTeam.TeamKey = "Management";
            managementTeam.DisplayName = "관리팀";
            managementTeam.IsActive = true;
            managementTeam.SortOrder = 20;

            OrganizationPosition requesterPosition = createPosition(planningTeam, "요청자", 10);
            addPermissionGrant(requesterPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(requesterPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.OwnTeam);

            OrganizationPosition leaderPosition = createPosition(planningTeam, "팀 결재자", 20);
            addPermissionGrant(leaderPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(leaderPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.OwnTeam);
            addPermissionGrant(leaderPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.OwnTeam);

            OrganizationPosition managerPosition = createPosition(managementTeam, "최종 결재자", 10);
            addPermissionGrant(managerPosition, PermissionKeys.APPROVAL_FINAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition directorPosition = createPosition(managementTeam, "관리 책임자", 20);
            addPermissionGrant(directorPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
            addPermissionGrant(directorPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.Global);
            addPermissionGrant(directorPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition deputyDirectorPosition = createPosition(managementTeam, "관리 부책임자", 30);
            addPermissionGrant(deputyDirectorPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition staffPosition = createPosition(managementTeam, "관리 담당자", 40);
            addPermissionGrant(staffPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.Global);
            addPermissionGrant(staffPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition recorderPosition = createPosition(managementTeam, "관리 기록자", 50);
            addPermissionGrant(recorderPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.Global);
            addPermissionGrant(recorderPosition, PermissionKeys.APPROVAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition secondManagerPosition = createPosition(managementTeam, "예비 최종 결재자", 60);
            addPermissionGrant(secondManagerPosition, PermissionKeys.APPROVAL_FINAL_APPROVE, EPermissionScopeType.Global);

            OrganizationPosition adminPosition = createPosition(managementTeam, "시스템 관리자", 70);
            adminPosition.IsSystemAdministrator = true;
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(managementTeam);
            dbContext.Positions.Add(requesterPosition);
            dbContext.Positions.Add(leaderPosition);
            dbContext.Positions.Add(managerPosition);
            dbContext.Positions.Add(directorPosition);
            dbContext.Positions.Add(deputyDirectorPosition);
            dbContext.Positions.Add(staffPosition);
            dbContext.Positions.Add(recorderPosition);
            dbContext.Positions.Add(secondManagerPosition);
            dbContext.Positions.Add(adminPosition);
            dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1, requesterPosition));
            dbContext.UserAccounts.Add(createUser(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1, leaderPosition));
            dbContext.UserAccounts.Add(createUser(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, 2, managerPosition));
            dbContext.UserAccounts.Add(createUser(4, "director", "디렉터", EStoredCompatibilityRole.ManagementDirector, 2, directorPosition));
            dbContext.UserAccounts.Add(createUser(5, "deputy.director", "부디렉터", EStoredCompatibilityRole.ManagementDeputyDirector, 2, deputyDirectorPosition));
            dbContext.UserAccounts.Add(createUser(6, "management.staff", "관리팀 담당자", EStoredCompatibilityRole.ManagementStaff, 2, staffPosition));
            dbContext.UserAccounts.Add(createUser(9, "management.recorder", "관리팀 기록자", EStoredCompatibilityRole.ManagementRecorder, 2, recorderPosition));
            if (finalApproverSeedMode == EFinalApproverSeedMode.SecondFinalApprover)
            {
                dbContext.UserAccounts.Add(createUser(7, "manager2", "매니저 담당", EStoredCompatibilityRole.ManagementManager, 2, secondManagerPosition));
            }

            dbContext.UserAccounts.Add(createUser(8, "admin", "사이트 관리자", EStoredCompatibilityRole.Admin, 2, adminPosition));

            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static ApprovalUserOption createApprovalUserOption(int id, string displayName, int? teamIdOrNull)
    {
        ApprovalUserOption option = new ApprovalUserOption();
        option.UserId = id;
        option.DisplayName = displayName;
        option.TeamId = teamIdOrNull;
        return option;
    }

    private static OrganizationPosition createPosition(Team team, string name, int sortOrder)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.Team = team;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        return position;
    }

    private static UserAccount createUser(
        int id,
        string username,
        string displayName,
        EStoredCompatibilityRole role,
        int? teamIdOrNull,
        OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "hash";
        userAccount.StoredCompatibilityRole = role;
        userAccount.TeamId = teamIdOrNull;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = System.DateTime.UtcNow;
        return userAccount;
    }

    private static FakeDriveService createApprovalDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("planning-team-folder", "기획팀", "official-root");
        driveService.AddFolder("planning-materials-folder", "기획팀 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-folder", "기획팀 결재 완료 자료", "planning-team-folder", "official-root");
        driveService.AddFile("planning-file", "2026-05-23_기획팀_부스운영안.pdf", "application/pdf", "planning-materials-folder", "planning-team-folder", "official-root");
        return driveService;
    }

    private static UserSession createSession(int userId, string username, string displayName, EStoredCompatibilityRole role, int? teamIdOrNull)
    {
        if (teamIdOrNull.HasValue)
        {
            OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(teamIdOrNull.Value);
            return new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(userId),
                    Username = Username.CreateOptional(username),
                    DisplayName = DisplayName.CreateOptional(displayName),
                    StoredCompatibilityRole = role,
                    OrganizationTeamIdOrNull = teamId,
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });
        }

        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(userId),
                Username = Username.CreateOptional(username),
                DisplayName = DisplayName.CreateOptional(displayName),
                StoredCompatibilityRole = role,
                OrganizationTeamIdOrNull = null,
                TeamKey = OrganizationTeamKey.Empty,
                TeamDisplayName = DisplayName.CreateOptional(string.Empty)
            });
    }

    private enum EFinalApproverSeedMode
    {
        SingleFinalApprover,
        SecondFinalApprover
    }
}
