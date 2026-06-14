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

public sealed class PositionManagementServiceTests
{
    [Fact]
    public async Task CreatePositionStoresAllowedPermissionGrantsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedTeamsAsync(dbContextFactory);
            PositionManagementService positionManagementService = new PositionManagementService(dbContextFactory);

            PositionFormModel formModel = new PositionFormModel();
            formModel.TeamId = 1;
            formModel.Name = "문서 관리자";
            formModel.Description = "팀 자료를 관리합니다.";
            formModel.SortOrder = 10;
            formModel.IsActive = true;
            PositionPermissionGrantFormModel permissionGrantFormModel = new PositionPermissionGrantFormModel();
            permissionGrantFormModel.PermissionKey = PermissionKeys.FILES_READ;
            permissionGrantFormModel.IsAllowed = true;
            permissionGrantFormModel.ScopeType = EPermissionScopeType.OwnTeam;
            formModel.PermissionGrants.Add(permissionGrantFormModel);

            ServiceResult result = await positionManagementService.CreatePositionAsync(formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                OrganizationPosition position = dbContext.Positions.Single();
                Assert.Equal("문서 관리자", position.Name);
                PositionPermissionGrant permissionGrant = dbContext.PositionPermissionGrants.Single();
                Assert.Equal(PermissionKeys.FILES_READ, permissionGrant.PermissionKey);
                Assert.Equal(EPermissionScopeType.OwnTeam, permissionGrant.ScopeType);
            }
        }
    }

    [Fact]
    public async Task SelectedTeamPermissionRequiresTargetTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedTeamsAsync(dbContextFactory);
            PositionManagementService positionManagementService = new PositionManagementService(dbContextFactory);

            PositionFormModel formModel = new PositionFormModel();
            formModel.TeamId = 1;
            formModel.Name = "외부 자료 담당";
            formModel.IsActive = true;
            PositionPermissionGrantFormModel permissionGrantFormModel = new PositionPermissionGrantFormModel();
            permissionGrantFormModel.PermissionKey = PermissionKeys.FILES_READ;
            permissionGrantFormModel.IsAllowed = true;
            permissionGrantFormModel.ScopeType = EPermissionScopeType.SelectedTeam;
            formModel.PermissionGrants.Add(permissionGrantFormModel);

            ServiceResult result = await positionManagementService.CreatePositionAsync(formModel, CancellationToken.None);

            Assert.False(result.IsSuccess);
        }
    }

    private static async Task seedTeamsAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 1;
            team.TeamKey = "Documents";
            team.DisplayName = "문서팀";
            team.IsActive = true;
            team.SortOrder = 10;
            dbContext.Teams.Add(team);

            Team targetTeam = new Team();
            targetTeam.Id = 2;
            targetTeam.TeamKey = "Operations";
            targetTeam.DisplayName = "운영팀";
            targetTeam.IsActive = true;
            targetTeam.SortOrder = 20;
            dbContext.Teams.Add(targetTeam);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
