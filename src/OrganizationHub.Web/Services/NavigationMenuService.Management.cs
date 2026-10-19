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

internal sealed partial class NavigationMenuService
{
    private const int EMPTY_SORT_ORDER_BASE = 0;
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
                .Where(menuItem => NavigationMenuRetirementPolicy.IsRetired(
                    NavigationMenuKey.CreateOptional(menuItem.MenuKey),
                    NavigationPath.CreateOptional(menuItem.Href)) == false)
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

                if (sectionOrNull.Revision != formModel.Revision)
                {
                    return AdministrativeSaveUtility.CreateConflictResult();
                }
                sectionOrNull.DisplayName = displayName;
                return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
            }

            NavigationMenuSection newSection = new NavigationMenuSection();
            newSection.SectionKey = "custom-" + Guid.NewGuid().ToString("N");
            newSection.DisplayName = displayName;
            newSection.SortOrder = await getNextSectionSortOrderAsync(dbContext, cancellationToken);
            dbContext.NavigationMenuSections.Add(newSection);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            if (OrderUpdateRequestValidation.hasDuplicateValues(request.OrderedSectionIds))
            {
                return ServiceResult.Fail("정렬할 메뉴 섹션이 중복되어 있습니다.");
            }

            List<int> orderedSectionIdValues = request.OrderedSectionIds.Select(sectionId => sectionId.GetRequiredValue()).ToList();
            List<NavigationMenuSection> sections = await dbContext.NavigationMenuSections
                .Where(section => orderedSectionIdValues.Contains(section.Id))
                .ToListAsync(cancellationToken);
            if (sections.Count != orderedSectionIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 메뉴 섹션을 찾을 수 없습니다.");
            }

            foreach (NavigationMenuSection item in sections)
            {
                int expectedRevision;
                if (request.ExpectedRevisions.TryGetValue(item.Id, out expectedRevision) == false || item.Revision != expectedRevision)
                {
                    return AdministrativeSaveUtility.CreateConflictResult();
                }
                dbContext.Entry(item).Property(value => value.Revision).IsModified = true;
            }

            Dictionary<int, NavigationMenuSection> sectionsById = sections.ToDictionary(section => section.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (NavigationMenuSectionId sectionId in request.OrderedSectionIds)
            {
                NavigationMenuSection section = sectionsById[sectionId.GetRequiredValue()];
                section.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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

        if (NavigationMenuPermissionKeys.ContainsUnknownPermissionKey(formModel.RequiredPermissionKeyValues))
        {
            return ServiceResult.Fail("지원하지 않는 필요 권한이 포함되어 있습니다.");
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

            bool hasSection = await dbContext.NavigationMenuSections.AnyAsync(section => section.Id == requiredSectionId, cancellationToken);
            if (hasSection == false)
            {
                return ServiceResult.Fail("메뉴 섹션을 찾을 수 없습니다.");
            }

            if (menuItemOrNull.Revision != formModel.Revision)
            {
                return AdministrativeSaveUtility.CreateConflictResult();
            }
            menuItemOrNull.DisplayName = formModel.DisplayName.Trim();
            menuItemOrNull.SectionId = requiredSectionId;
            menuItemOrNull.SortOrder = formModel.SortOrder;
            menuItemOrNull.AccessLevel = menuItemOrNull.IsLocked ? menuItemOrNull.AccessLevel : formModel.AccessLevel;
            menuItemOrNull.RequiredPermissionKeys = menuItemOrNull.IsLocked ? menuItemOrNull.RequiredPermissionKeys : NavigationMenuPermissionKeys.CreateStorageValue(formModel.RequiredPermissionKeyValues);
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

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            if (OrderUpdateRequestValidation.hasDuplicateValues(request.OrderedMenuItemIds))
            {
                return ServiceResult.Fail("정렬할 메뉴가 중복되어 있습니다.");
            }

            List<int> orderedMenuItemIdValues = request.OrderedMenuItemIds.Select(menuItemId => menuItemId.GetRequiredValue()).ToList();
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Where(menuItem => orderedMenuItemIdValues.Contains(menuItem.Id))
                .ToListAsync(cancellationToken);
            if (menuItems.Count != orderedMenuItemIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 메뉴를 찾을 수 없습니다.");
            }

            foreach (NavigationMenuItem item in menuItems)
            {
                int expectedRevision;
                if (request.ExpectedRevisions.TryGetValue(item.Id, out expectedRevision) == false || item.Revision != expectedRevision)
                {
                    return AdministrativeSaveUtility.CreateConflictResult();
                }
                dbContext.Entry(item).Property(value => value.Revision).IsModified = true;
            }

            Dictionary<int, NavigationMenuItem> menuItemsById = menuItems.ToDictionary(menuItem => menuItem.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (NavigationMenuItemId menuItemId in request.OrderedMenuItemIds)
            {
                NavigationMenuItem menuItem = menuItemsById[menuItemId.GetRequiredValue()];
                menuItem.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    private async Task<int> getNextSectionSortOrderAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        int? maximumSortOrderOrNull = await dbContext.NavigationMenuSections.Select(section => (int?)section.SortOrder).MaxAsync(cancellationToken);
        int maximumSortOrder = EMPTY_SORT_ORDER_BASE;
        if (maximumSortOrderOrNull.HasValue)
        {
            maximumSortOrder = maximumSortOrderOrNull.GetValueOrDefault();
        }

        return maximumSortOrder + SORT_ORDER_STEP;
    }
}
