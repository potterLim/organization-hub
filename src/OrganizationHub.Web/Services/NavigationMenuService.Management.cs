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
    private const int SORT_ORDER_STEP = 10;

    public async Task<IReadOnlyList<NavigationMenuItem>> GetManageableMenuItemsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.TeamPermissions)
                .ThenInclude(menuPermission => menuPermission.Team)
                .ToListAsync(cancellationToken);
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
            NavigationMenuItemId menuItemId = formModel.NavigationMenuItemIdValue;
            int requiredMenuItemId = menuItemId.GetRequiredValue();
            NavigationMenuItem? menuItemOrNull = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.TeamPermissions)
                .FirstOrDefaultAsync(menuItem => menuItem.Id == requiredMenuItemId, cancellationToken);
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
                IReadOnlyList<int> selectedTeamIdValues = formModel.TeamIdValues.Select(teamId => teamId.GetRequiredValue()).ToList();
                List<int> activeTeamIdValues = await dbContext.Teams
                    .Where(team => team.IsActive && selectedTeamIdValues.Contains(team.Id))
                    .Select(team => team.Id)
                    .ToListAsync(cancellationToken);
                foreach (int teamIdValue in activeTeamIdValues.Distinct())
                {
                    NavigationMenuTeamPermission permission = new NavigationMenuTeamPermission();
                    permission.NavigationMenuItemId = menuItemOrNull.Id;
                    permission.TeamId = teamIdValue;
                    dbContext.NavigationMenuTeamPermissions.Add(permission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateMenuItemOrderAsync(NavigationMenuItemOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedMenuItemIds.Count == 0)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<int> orderedMenuItemIdValues = request.OrderedMenuItemIds.Select(menuItemId => menuItemId.GetRequiredValue()).Distinct().ToList();
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Where(menuItem => orderedMenuItemIdValues.Contains(menuItem.Id))
                .ToListAsync(cancellationToken);
            if (menuItems.Count != orderedMenuItemIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 메뉴를 찾을 수 없습니다.");
            }

            Dictionary<int, NavigationMenuItem> menuItemsById = menuItems.ToDictionary(menuItem => menuItem.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (NavigationMenuItemId menuItemId in request.OrderedMenuItemIds)
            {
                NavigationMenuItem menuItem = menuItemsById[menuItemId.GetRequiredValue()];
                menuItem.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }
}
