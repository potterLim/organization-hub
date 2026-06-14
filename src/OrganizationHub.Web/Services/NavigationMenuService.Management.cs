using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class NavigationMenuService
{
    public async Task<IReadOnlyList<NavigationMenuItem>> GetManageableMenuItemsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).ThenInclude(menuPermission => menuPermission.Team).ToListAsync(cancellationToken);
            List<NavigationMenuItem> manageableMenuItems = menuItems
                .Where(menuItem => NavigationMenuRetirementPolicy.IsRetired(menuItem.MenuKey, menuItem.Href) == false)
                .OrderBy(menuItem => menuItem.SortOrder)
                .ThenBy(menuItem => menuItem.DisplayName)
                .ToList();
            return manageableMenuItems;
        }
    }

    public async Task<ServiceResult> UpdateMenuItemAsync(NavigationMenuItemFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("메뉴 표시 이름을 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            NavigationMenuItem? menuItemOrNull = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).FirstOrDefaultAsync(menuItem => menuItem.ID == formModel.ID, cancellationToken);
            if (menuItemOrNull == null)
            {
                return ServiceResult.Fail("메뉴 항목을 찾을 수 없습니다.");
            }

            menuItemOrNull.DisplayName = formModel.DisplayName.Trim();
            menuItemOrNull.SortOrder = formModel.SortOrder;
            menuItemOrNull.AccessLevel = menuItemOrNull.IsLocked ? menuItemOrNull.AccessLevel : formModel.AccessLevel;
            menuItemOrNull.IsActive = menuItemOrNull.IsLocked || formModel.IsActive;
            menuItemOrNull.IsVisibleToAllTeams = MenuAccessLevelUtility.IsTeamScoped(menuItemOrNull.AccessLevel) && formModel.IsVisibleToAllTeams;
            dbContext.NavigationMenuTeamPermissions.RemoveRange(menuItemOrNull.TeamPermissions);
            if (MenuAccessLevelUtility.IsTeamScoped(menuItemOrNull.AccessLevel) && menuItemOrNull.IsVisibleToAllTeams == false)
            {
                List<int> activeTeamIDs = await dbContext.Teams.Where(team => team.IsActive && formModel.TeamIDs.Contains(team.ID)).Select(team => team.ID).ToListAsync(cancellationToken);
                foreach (int teamID in activeTeamIDs.Distinct())
                {
                    NavigationMenuTeamPermission permission = new NavigationMenuTeamPermission();
                    permission.NavigationMenuItemID = menuItemOrNull.ID;
                    permission.TeamID = teamID;
                    dbContext.NavigationMenuTeamPermissions.Add(permission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }
}
