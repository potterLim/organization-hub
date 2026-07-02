using System.Collections.Generic;
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

    [Fact]
    public async Task UpdatePositionOrderRejectsDuplicatePositionIdsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedTeamsAsync(dbContextFactory);
            await seedPositionsAsync(dbContextFactory);
            PositionManagementService positionManagementService = new PositionManagementService(dbContextFactory);
            List<OrganizationPositionId> orderedPositionIds = new List<OrganizationPositionId>
            {
                OrganizationPositionId.CreateExisting(1),
                OrganizationPositionId.CreateExisting(1)
            };

            ServiceResult result = await positionManagementService.UpdatePositionOrderAsync(
                new OrganizationPositionOrderUpdateRequest(OrganizationTeamId.CreateExisting(1), orderedPositionIds),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("정렬할 직책이 중복되어 있습니다.", result.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Equal(10, dbContext.Positions.Single(position => position.Id == 1).SortOrder);
                Assert.Equal(20, dbContext.Positions.Single(position => position.Id == 2).SortOrder);
            }
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

    private static async Task seedPositionsAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition firstPosition = new OrganizationPosition();
            firstPosition.Id = 1;
            firstPosition.TeamId = 1;
            firstPosition.Name = "문서 담당";
            firstPosition.IsActive = true;
            firstPosition.SortOrder = 10;
            dbContext.Positions.Add(firstPosition);

            OrganizationPosition secondPosition = new OrganizationPosition();
            secondPosition.Id = 2;
            secondPosition.TeamId = 1;
            secondPosition.Name = "문서 책임자";
            secondPosition.IsActive = true;
            secondPosition.SortOrder = 20;
            dbContext.Positions.Add(secondPosition);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
