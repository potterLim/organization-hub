using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static class NavigationMenuSeedUpgradePolicy
{
    private const int UNKNOWN_PREVIOUS_DEFAULT_SORT_ORDER = -1;
    private const int PREVIOUS_MATERIALS_DEFAULT_SORT_ORDER = 20;
    private const int PREVIOUS_MANAGEMENT_FILES_DEFAULT_SORT_ORDER = 30;
    private const int PREVIOUS_COMMON_DEFAULT_SORT_ORDER = 40;
    private const int PREVIOUS_PUBLIC_DEFAULT_SORT_ORDER = 50;
    private const int PREVIOUS_UPLOAD_DEFAULT_SORT_ORDER = 60;
    private const int PREVIOUS_APPROVAL_REQUEST_DEFAULT_SORT_ORDER = 70;
    private const int PREVIOUS_APPROVAL_INBOX_DEFAULT_SORT_ORDER = 80;
    private const int PREVIOUS_APPROVAL_HISTORY_DEFAULT_SORT_ORDER = 90;
    private const int PREVIOUS_APPROVED_FILES_DEFAULT_SORT_ORDER = 100;
    private const int PREVIOUS_CALENDAR_DEFAULT_SORT_ORDER = 110;
    private const int PREVIOUS_TODOS_DEFAULT_SORT_ORDER = 120;
    private const int PREVIOUS_RESPONSIBLE_RESOURCES_DEFAULT_SORT_ORDER = 130;
    private const int PREVIOUS_WORK_GUIDELINES_DEFAULT_SORT_ORDER = 140;
    private const int PREVIOUS_WORK_RESOURCES_DEFAULT_SORT_ORDER = 150;
    private const int PREVIOUS_ADMIN_USERS_DEFAULT_SORT_ORDER = 210;
    private const int PREVIOUS_ADMIN_TEAMS_DEFAULT_SORT_ORDER = 220;
    private const int PREVIOUS_ADMIN_POSITIONS_DEFAULT_SORT_ORDER = 225;
    private const int PREVIOUS_ADMIN_APPROVAL_WORKFLOWS_DEFAULT_SORT_ORDER = 228;
    private const int PREVIOUS_ADMIN_SETTINGS_DEFAULT_SORT_ORDER = 230;
    private const int PREVIOUS_ADMIN_CALENDAR_AUDIT_DEFAULT_SORT_ORDER = 235;
    private const int PREVIOUS_ADMIN_GOOGLE_DEFAULT_SORT_ORDER = 240;
    private const int PREVIOUS_ADMIN_MENUS_DEFAULT_SORT_ORDER = 250;

    private const int LEGACY_MATERIALS_SORT_ORDER = 30;
    private const int LEGACY_CALENDAR_SORT_ORDER = 70;
    private const int LEGACY_RESPONSIBLE_RESOURCES_SORT_ORDER = 80;
    private const int LEGACY_WORK_GUIDELINES_SORT_ORDER = 90;
    private const int LEGACY_WORK_RESOURCES_SORT_ORDER = 100;
    private const int LEGACY_ARCHIVE_SORT_ORDER = 110;
    private const int LEGACY_RESPONSIBLE_RESOURCES_AFTER_TODOS_SORT_ORDER = 120;
    private const int LEGACY_WORK_GUIDELINES_AFTER_RESPONSIBLE_SORT_ORDER = 130;
    private const int LEGACY_WORK_RESOURCES_AFTER_GUIDELINES_SORT_ORDER = 140;
    private const int LEGACY_ADMIN_MENUS_SORT_ORDER = 240;

    public static void DeactivateRetiredMenus(IReadOnlyList<NavigationMenuItem> existingMenuItems)
    {
        foreach (NavigationMenuItem existingMenuItem in existingMenuItems)
        {
            if (NavigationMenuRetirementPolicy.IsRetired(NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey), NavigationPath.CreateOptional(existingMenuItem.Href)))
            {
                existingMenuItem.IsActive = false;
            }
        }
    }

    public static void ApplyStoredMenuUpgrades(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        if (shouldUpgradeDefaultMenuAccessLevel(existingMenuItem, defaultMenuItem))
        {
            existingMenuItem.AccessLevel = defaultMenuItem.AccessLevel;
        }

        if (shouldApplyCurrentDefaultMenuAccessLevel(existingMenuItem))
        {
            existingMenuItem.AccessLevel = defaultMenuItem.AccessLevel;
        }

        upgradeStoredDefaultMenuSortOrder(existingMenuItem, defaultMenuItem);
        upgradeStoredDefaultMenuDisplayName(existingMenuItem, defaultMenuItem);
        upgradeStoredDefaultMenuPermissionKeys(existingMenuItem, defaultMenuItem);
    }

    private static bool shouldUpgradeDefaultMenuAccessLevel(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        if (existingMenuItem.AccessLevel != EMenuAccessLevel.Authenticated)
        {
            return false;
        }

        if (defaultMenuItem.AccessLevel != EMenuAccessLevel.TeamMember)
        {
            return false;
        }

        NavigationMenuKey existingMenuKey = NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey);
        string menuKey = existingMenuKey.GetOptionalValue();
        return menuKey == NavigationMenuKey.MATERIALS || menuKey == NavigationMenuKey.UPLOAD;
    }

    private static bool shouldApplyCurrentDefaultMenuAccessLevel(NavigationMenuItem existingMenuItem)
    {
        NavigationMenuKey existingMenuKey = NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey);
        string menuKey = existingMenuKey.GetOptionalValue();
        return menuKey == NavigationMenuKey.ADMIN_USERS ||
            menuKey == NavigationMenuKey.ADMIN_TEAMS ||
            menuKey == NavigationMenuKey.ADMIN_POSITIONS ||
            menuKey == NavigationMenuKey.ADMIN_APPROVAL_WORKFLOWS ||
            menuKey == NavigationMenuKey.ADMIN_SETTINGS ||
            menuKey == NavigationMenuKey.ADMIN_CALENDAR_AUDIT;
    }

    private static void upgradeStoredDefaultMenuSortOrder(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        NavigationMenuKey menuKey = NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey);
        int previousDefaultSortOrder = getPreviousDefaultMenuSortOrder(menuKey);
        if (previousDefaultSortOrder != UNKNOWN_PREVIOUS_DEFAULT_SORT_ORDER && existingMenuItem.SortOrder == previousDefaultSortOrder)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (hasPreviousSortOrder(menuKey, existingMenuItem.SortOrder))
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
        }
    }

    private static bool hasPreviousSortOrder(NavigationMenuKey menuKey, int sortOrder)
    {
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.RESPONSIBLE_RESOURCES:
                return isPreviousSortOrder(
                    sortOrder,
                    LEGACY_RESPONSIBLE_RESOURCES_SORT_ORDER,
                    LEGACY_RESPONSIBLE_RESOURCES_AFTER_TODOS_SORT_ORDER);
            case NavigationMenuKey.WORK_GUIDELINES:
                return isPreviousSortOrder(
                    sortOrder,
                    LEGACY_RESPONSIBLE_RESOURCES_SORT_ORDER,
                    LEGACY_WORK_GUIDELINES_SORT_ORDER,
                    LEGACY_WORK_GUIDELINES_AFTER_RESPONSIBLE_SORT_ORDER);
            case NavigationMenuKey.WORK_RESOURCES:
                return isPreviousSortOrder(
                    sortOrder,
                    LEGACY_WORK_GUIDELINES_SORT_ORDER,
                    LEGACY_WORK_RESOURCES_SORT_ORDER,
                    LEGACY_WORK_RESOURCES_AFTER_GUIDELINES_SORT_ORDER);
            case NavigationMenuKey.MATERIALS:
                return isPreviousSortOrder(sortOrder, LEGACY_MATERIALS_SORT_ORDER);
            case NavigationMenuKey.CALENDAR:
                return isPreviousSortOrder(sortOrder, LEGACY_CALENDAR_SORT_ORDER);
            case NavigationMenuKey.ARCHIVE:
                return isPreviousSortOrder(sortOrder, LEGACY_WORK_RESOURCES_SORT_ORDER, LEGACY_ARCHIVE_SORT_ORDER);
            case NavigationMenuKey.ADMIN_MENUS:
                return isPreviousSortOrder(sortOrder, LEGACY_ADMIN_MENUS_SORT_ORDER);
            default:
                return false;
        }
    }

    private static bool isPreviousSortOrder(int sortOrder, params int[] previousSortOrders)
    {
        foreach (int previousSortOrder in previousSortOrders)
        {
            if (sortOrder == previousSortOrder)
            {
                return true;
            }
        }

        return false;
    }

    private static int getPreviousDefaultMenuSortOrder(NavigationMenuKey menuKey)
    {
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.MATERIALS:
                return PREVIOUS_MATERIALS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.MANAGEMENT_FILES:
                return PREVIOUS_MANAGEMENT_FILES_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.COMMON:
                return PREVIOUS_COMMON_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.PUBLIC:
                return PREVIOUS_PUBLIC_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.UPLOAD:
                return PREVIOUS_UPLOAD_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.APPROVAL_REQUEST:
                return PREVIOUS_APPROVAL_REQUEST_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.APPROVAL_INBOX:
                return PREVIOUS_APPROVAL_INBOX_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.APPROVAL_HISTORY:
                return PREVIOUS_APPROVAL_HISTORY_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.APPROVED_FILES:
                return PREVIOUS_APPROVED_FILES_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.CALENDAR:
                return PREVIOUS_CALENDAR_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.TODOS:
                return PREVIOUS_TODOS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.RESPONSIBLE_RESOURCES:
                return PREVIOUS_RESPONSIBLE_RESOURCES_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.WORK_GUIDELINES:
                return PREVIOUS_WORK_GUIDELINES_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.WORK_RESOURCES:
                return PREVIOUS_WORK_RESOURCES_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_USERS:
                return PREVIOUS_ADMIN_USERS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_TEAMS:
                return PREVIOUS_ADMIN_TEAMS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_POSITIONS:
                return PREVIOUS_ADMIN_POSITIONS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_APPROVAL_WORKFLOWS:
                return PREVIOUS_ADMIN_APPROVAL_WORKFLOWS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_SETTINGS:
                return PREVIOUS_ADMIN_SETTINGS_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_CALENDAR_AUDIT:
                return PREVIOUS_ADMIN_CALENDAR_AUDIT_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_GOOGLE:
                return PREVIOUS_ADMIN_GOOGLE_DEFAULT_SORT_ORDER;
            case NavigationMenuKey.ADMIN_MENUS:
                return PREVIOUS_ADMIN_MENUS_DEFAULT_SORT_ORDER;
            default:
                return UNKNOWN_PREVIOUS_DEFAULT_SORT_ORDER;
        }
    }

    private static void upgradeStoredDefaultMenuDisplayName(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        if (isPreviousDefaultMenuDisplayName(NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey), existingMenuItem.DisplayName))
        {
            existingMenuItem.DisplayName = defaultMenuItem.DisplayName;
        }
    }

    private static void upgradeStoredDefaultMenuPermissionKeys(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        NavigationMenuKey menuKey = NavigationMenuKey.CreateOptional(existingMenuItem.MenuKey);
        if (isPreviousDefaultMenuPermissionKeys(menuKey, existingMenuItem.RequiredPermissionKeys))
        {
            existingMenuItem.RequiredPermissionKeys = defaultMenuItem.RequiredPermissionKeys;
        }
    }

    private static bool isPreviousDefaultMenuDisplayName(NavigationMenuKey menuKey, string displayName)
    {
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.MANAGEMENT_FILES:
                return displayName == "관리팀 자료" || displayName == "조직 관리 자료";
            case NavigationMenuKey.MATERIALS:
                return displayName == "자료" || displayName == "팀별 자료";
            case NavigationMenuKey.COMMON:
                return displayName == "공통";
            case NavigationMenuKey.PUBLIC:
                return displayName == "공개" || displayName == "공개 자료" || displayName == "외부 배포자료";
            case NavigationMenuKey.UPLOAD:
                return displayName == "업로드" || displayName == "파일 업로드";
            case NavigationMenuKey.RESPONSIBLE_RESOURCES:
                return displayName == "책임자 공유";
            case NavigationMenuKey.WORK_GUIDELINES:
                return displayName == "규칙" || displayName == "업무 규칙";
            case NavigationMenuKey.WORK_RESOURCES:
                return displayName == "업무 자료";
            case NavigationMenuKey.ARCHIVE:
                return displayName == "보관" || displayName == "아카이브";
            case NavigationMenuKey.ADMIN_USERS:
                return displayName == "사용자";
            case NavigationMenuKey.ADMIN_TEAMS:
                return displayName == "팀";
            case NavigationMenuKey.ADMIN_POSITIONS:
                return displayName == "직책" || displayName == "권한" || displayName == "직책/권한 관리";
            case NavigationMenuKey.ADMIN_APPROVAL_WORKFLOWS:
                return displayName == "결재선" || displayName == "결재 경로" || displayName == "결재 경로 관리";
            case NavigationMenuKey.ADMIN_SETTINGS:
                return displayName == "설정" || displayName == "전역 설정" || displayName == "시스템 설정";
            case NavigationMenuKey.ADMIN_CALENDAR_AUDIT:
                return displayName == "일정 기록" || displayName == "삭제 일정 기록";
            case NavigationMenuKey.ADMIN_GOOGLE:
                return displayName == "Google" || displayName == "Google 연결";
            case NavigationMenuKey.ADMIN_MENUS:
                return displayName == "메뉴" || displayName == "메뉴 관리";
            default:
                return false;
        }
    }

    private static bool isPreviousDefaultMenuPermissionKeys(NavigationMenuKey menuKey, string permissionKeys)
    {
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.APPROVAL_HISTORY:
                return permissionKeys == NavigationMenuPermissionKeys.CreateStorageValue(
                    new PermissionKey[] { new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ) });
            default:
                return false;
        }
    }
}
