using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static class NavigationMenuSeedUpgradePolicy
{
    public static void DeactivateRetiredMenus(IReadOnlyList<NavigationMenuItem> existingMenuItems)
    {
        foreach (NavigationMenuItem existingMenuItem in existingMenuItems)
        {
            if (NavigationMenuRetirementPolicy.IsRetired(existingMenuItem.MenuKey, existingMenuItem.Href))
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

        return existingMenuItem.MenuKey == "Materials" || existingMenuItem.MenuKey == "Upload";
    }

    private static bool shouldApplyCurrentDefaultMenuAccessLevel(NavigationMenuItem existingMenuItem)
    {
        return existingMenuItem.MenuKey == "AdminUsers" ||
            existingMenuItem.MenuKey == "AdminTeams" ||
            existingMenuItem.MenuKey == "AdminPositions" ||
            existingMenuItem.MenuKey == "AdminApprovalWorkflows" ||
            existingMenuItem.MenuKey == "AdminSettings" ||
            existingMenuItem.MenuKey == "AdminCalendarAudit";
    }

    private static void upgradeStoredDefaultMenuSortOrder(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        int previousDefaultSortOrder = getPreviousDefaultMenuSortOrder(existingMenuItem.MenuKey);
        if (previousDefaultSortOrder >= 0 && existingMenuItem.SortOrder == previousDefaultSortOrder)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (hasPreviousSortOrder(existingMenuItem.MenuKey, existingMenuItem.SortOrder))
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
        }
    }

    private static bool hasPreviousSortOrder(string menuKey, int sortOrder)
    {
        switch (menuKey)
        {
            case "ResponsibleResources":
                return sortOrder == 80 || sortOrder == 120;
            case "WorkGuidelines":
                return sortOrder == 80 || sortOrder == 90 || sortOrder == 130;
            case "WorkResources":
                return sortOrder == 90 || sortOrder == 100 || sortOrder == 140;
            case "Materials":
                return sortOrder == 30;
            case "Calendar":
                return sortOrder == 70;
            case "Archive":
                return sortOrder == 100 || sortOrder == 110;
            case "AdminMenus":
                return sortOrder == 240;
            default:
                return false;
        }
    }

    private static int getPreviousDefaultMenuSortOrder(string menuKey)
    {
        switch (menuKey)
        {
            case "Materials":
                return 20;
            case "ManagementFiles":
                return 30;
            case "Common":
                return 40;
            case "Public":
                return 50;
            case "Upload":
                return 60;
            case "ApprovalRequest":
                return 70;
            case "ApprovalInbox":
                return 80;
            case "ApprovalHistory":
                return 90;
            case "ApprovedFiles":
                return 100;
            case "Calendar":
                return 110;
            case "Todos":
                return 120;
            case "ResponsibleResources":
                return 130;
            case "WorkGuidelines":
                return 140;
            case "WorkResources":
                return 150;
            case "AdminUsers":
                return 210;
            case "AdminTeams":
                return 220;
            case "AdminPositions":
                return 225;
            case "AdminApprovalWorkflows":
                return 228;
            case "AdminSettings":
                return 230;
            case "AdminCalendarAudit":
                return 235;
            case "AdminGoogle":
                return 240;
            case "AdminMenus":
                return 250;
            default:
                return -1;
        }
    }

    private static void upgradeStoredDefaultMenuDisplayName(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        if (isPreviousDefaultMenuDisplayName(existingMenuItem.MenuKey, existingMenuItem.DisplayName))
        {
            existingMenuItem.DisplayName = defaultMenuItem.DisplayName;
        }
    }

    private static bool isPreviousDefaultMenuDisplayName(string menuKey, string displayName)
    {
        switch (menuKey)
        {
            case "ManagementFiles":
                return displayName == "관리팀 자료" || displayName == "조직 관리 자료";
            case "Materials":
                return displayName == "자료" || displayName == "팀별 자료";
            case "Common":
                return displayName == "공통";
            case "Public":
                return displayName == "공개" || displayName == "공개 자료" || displayName == "외부 배포자료";
            case "Upload":
                return displayName == "업로드" || displayName == "파일 업로드";
            case "ResponsibleResources":
                return displayName == "책임자 공유";
            case "WorkGuidelines":
                return displayName == "규칙" || displayName == "업무 규칙";
            case "WorkResources":
                return displayName == "업무 자료";
            case "Archive":
                return displayName == "보관" || displayName == "아카이브";
            case "AdminUsers":
                return displayName == "사용자";
            case "AdminTeams":
                return displayName == "팀";
            case "AdminPositions":
                return displayName == "직책" || displayName == "권한" || displayName == "직책/권한 관리";
            case "AdminApprovalWorkflows":
                return displayName == "결재선" || displayName == "결재 경로" || displayName == "결재 경로 관리";
            case "AdminSettings":
                return displayName == "설정" || displayName == "전역 설정" || displayName == "시스템 설정";
            case "AdminCalendarAudit":
                return displayName == "일정 기록" || displayName == "삭제 일정 기록";
            case "AdminGoogle":
                return displayName == "Google" || displayName == "Google 연결";
            case "AdminMenus":
                return displayName == "메뉴" || displayName == "메뉴 관리";
            default:
                return false;
        }
    }
}
