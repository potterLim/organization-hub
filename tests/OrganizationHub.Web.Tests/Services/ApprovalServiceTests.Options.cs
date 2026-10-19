using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    [Fact]
    public void DefaultLinePolicyChoosesFinalApproverThatCanCompleteSelectedStep()
    {
        List<ApprovalUserOption> approverOptions = new List<ApprovalUserOption>
        {
            createApprovalUserOption(5, "부디렉터", null),
            createApprovalUserOption(6, "관리팀 담당자", null)
        };
        List<ApprovalUserOption> finalApproverOptions = new List<ApprovalUserOption>
        {
            createApprovalUserOption(5, "부디렉터", null),
            createApprovalUserOption(3, "매니저", null)
        };

        IReadOnlyList<ApprovalDefaultLineOption> defaultLines = ApprovalDefaultLinePolicy.CreateDefaultLineOptions(approverOptions, finalApproverOptions, new ApprovalStepCount[] { new ApprovalStepCount(3) });
        ApprovalDefaultLineOption defaultLine = defaultLines.Single();

        Assert.Equal(5, defaultLine.FirstApproverUserId);
        Assert.Equal(6, defaultLine.SecondApproverUserId);
        Assert.Equal(3, defaultLine.FinalApproverUserId);
    }

    [Fact]
    public async Task ApprovalOptionsComeFromConfiguredApproverPermissionsAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);

                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 2);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 4);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 5);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 6);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserId == 3);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserId == 8);
                Assert.Single(createContext.FinalApproverOptions);
                Assert.Equal(3, createContext.FinalApproverOptions[0].UserId);
            }
        }
    }

    [Fact]
    public async Task AdminCannotCreateApprovalRequestAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                OrganizationPosition adminPosition = new OrganizationPosition();
                adminPosition.TeamId = 1;
                adminPosition.Name = "관리자 직책";
                adminPosition.Description = string.Empty;
                adminPosition.SortOrder = 10;
                adminPosition.IsActive = true;
                addPermissionGrant(adminPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.Global);
                UserAccount adminUser = dbContext.UserAccounts.Single(user => user.Id == 8);
                adminUser.Position = adminPosition;
                dbContext.Positions.Add(adminPosition);
                dbContext.SaveChanges();
            }

            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession adminSession = createSession(8, "admin", "사이트 관리자", EStoredCompatibilityRole.Admin, null);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(adminSession, CancellationToken.None);

                Assert.False(createContext.CanCreateRequest);
                Assert.Equal("결재할 자료 위치가 연결되어 있지 않습니다.", createContext.BlockReason);
                Assert.Equal(EApprovalSetupArea.Resources, createContext.SetupArea);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 4);
                Assert.Contains(createContext.FinalApproverOptions, option => option.UserId == 3);
            }
        }
    }
}
