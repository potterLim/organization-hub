using System;
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

    public async Task<IReadOnlyList<NavigationMenuSection>> GetMenuSectionsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuSection> sections = await dbContext.NavigationMenuSections
                .OrderBy(section => section.SortOrder)
                .ThenBy(section => section.DisplayName)
                .ToListAsync(cancellationToken);
            return sections;
        }
    }

    public async Task<IReadOnlyList<NavigationMenuItem>> GetManageableMenuItemsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.Section)
                .Include(menuItem => menuItem.TeamPermissions)
                .ThenInclude(menuPermission => menuPermission.Team)
                .ToListAsync(cancellationToken);
            List<NavigationMenuItem> manageableMenuItems = menuItems
                .Where(menuItem => NavigationMenuRetirementPolicy.IsRetired(menuItem.MenuKey, menuItem.Href) == false)
                .OrderBy(menuItem => menuItem.Section == null ? int.MaxValue : menuItem.Section.SortOrder)
                .ThenBy(menuItem => menuItem.SortOrder)
                .ThenBy(menuItem => menuItem.DisplayName)
                .ToList();
            return manageableMenuItems;
        }
    }

    public async Task<ServiceResult> SaveMenuSectionAsync(NavigationMenuSectionFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("섹션 이름을 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string displayName = formModel.DisplayName.Trim();
            NavigationMenuSectionId sectionId = formModel.NavigationMenuSectionIdValue;
            if (sectionId.HasValue)
            {
                int requiredSectionId = sectionId.GetRequiredValue();
                NavigationMenuSection? sectionOrNull = await dbContext.NavigationMenuSections.FirstOrDefaultAsync(section => section.Id == requiredSectionId, cancellationToken);
                if (sectionOrNull == null)
                {
                    return ServiceResult.Fail("메뉴 섹션을 찾을 수 없습니다.");
                }

                sectionOrNull.DisplayName = displayName;
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult.Succeed();
            }

            NavigationMenuSection newSection = new NavigationMenuSection();
            newSection.SectionKey = "custom-" + Guid.NewGuid().ToString("N");
            newSection.DisplayName = displayName;
            newSection.SortOrder = await getNextSectionSortOrderAsync(dbContext, cancellationToken);
            dbContext.NavigationMenuSections.Add(newSection);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateMenuSectionOrderAsync(NavigationMenuSectionOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedSectionIds.Count == 0)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<int> orderedSectionIdValues = request.OrderedSectionIds.Select(sectionId => sectionId.GetRequiredValue()).Distinct().ToList();
            List<NavigationMenuSection> sections = await dbContext.NavigationMenuSections
                .Where(section => orderedSectionIdValues.Contains(section.Id))
                .ToListAsync(cancellationToken);
            if (sections.Count != orderedSectionIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 메뉴 섹션을 찾을 수 없습니다.");
            }

            Dictionary<int, NavigationMenuSection> sectionsById = sections.ToDictionary(section => section.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (NavigationMenuSectionId sectionId in request.OrderedSectionIds)
            {
                NavigationMenuSection section = sectionsById[sectionId.GetRequiredValue()];
                section.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateMenuItemAsync(NavigationMenuItemFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("메뉴 표시 이름을 입력해 주세요.");
        }

        if (formModel.NavigationMenuSectionIdValue.HasValue == false)
        {
            return ServiceResult.Fail("메뉴 섹션을 선택해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            NavigationMenuItemId menuItemId = formModel.NavigationMenuItemIdValue;
            int requiredMenuItemId = menuItemId.GetRequiredValue();
            int requiredSectionId = formModel.NavigationMenuSectionIdValue.GetRequiredValue();
            NavigationMenuItem? menuItemOrNull = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.TeamPermissions)
                .FirstOrDefaultAsync(menuItem => menuItem.Id == requiredMenuItemId, cancellationToken);
            if (menuItemOrNull == null)
            {
                return ServiceResult.Fail("메뉴 항목을 찾을 수 없습니다.");
            }

            bool sectionExists = await dbContext.NavigationMenuSections.AnyAsync(section => section.Id == requiredSectionId, cancellationToken);
            if (sectionExists == false)
            {
                return ServiceResult.Fail("메뉴 섹션을 찾을 수 없습니다.");
            }

            menuItemOrNull.DisplayName = formModel.DisplayName.Trim();
            menuItemOrNull.SectionId = requiredSectionId;
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

    private async Task<int> getNextSectionSortOrderAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        int maximumSortOrder = await dbContext.NavigationMenuSections.Select(section => (int?)section.SortOrder).MaxAsync(cancellationToken) ?? 0;
        return maximumSortOrder + SORT_ORDER_STEP;
    }
}
