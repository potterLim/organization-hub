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

public sealed class TeamManagementServiceTests
{
    [Fact]
    public async Task UpdateTeamOrderRejectsDuplicateTeamIdsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedTeamsAsync(dbContextFactory);
            TeamManagementService teamManagementService = new TeamManagementService(dbContextFactory);
            List<OrganizationTeamId> orderedTeamIds = new List<OrganizationTeamId>
            {
                OrganizationTeamId.CreateExisting(1),
                OrganizationTeamId.CreateExisting(1)
            };

            ServiceResult result = await teamManagementService.UpdateTeamOrderAsync(new OrganizationTeamOrderUpdateRequest(orderedTeamIds), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("정렬할 팀이 중복되어 있습니다.", result.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Equal(10, dbContext.Teams.Single(team => team.Id == 1).SortOrder);
                Assert.Equal(20, dbContext.Teams.Single(team => team.Id == 2).SortOrder);
            }
        }
    }

    private static async Task seedTeamsAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Teams.Add(createTeam(1, "Planning", "기획팀", 10));
            dbContext.Teams.Add(createTeam(2, "Operation", "운영팀", 20));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static Team createTeam(int id, string teamKey, string displayName, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }
}
