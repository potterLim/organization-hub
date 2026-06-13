using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDataSeeder
{
    private async Task seedNavigationMenusAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        List<NavigationMenuItem> defaultMenuItems = createDefaultMenuItems();
        List<NavigationMenuItem> existingMenuItems = await dbContext.NavigationMenuItems.ToListAsync(cancellationToken);
        deactivateRemovedDefaultMenus(existingMenuItems);
        foreach (NavigationMenuItem defaultMenuItem in defaultMenuItems)
        {
            NavigationMenuItem? existingMenuItemOrNull = existingMenuItems.FirstOrDefault(menuItem => menuItem.MenuKey == defaultMenuItem.MenuKey);
            if (existingMenuItemOrNull == null)
            {
                dbContext.NavigationMenuItems.Add(defaultMenuItem);
                continue;
            }

            existingMenuItemOrNull.Href = defaultMenuItem.Href;
            existingMenuItemOrNull.IconKey = defaultMenuItem.IconKey;
            existingMenuItemOrNull.IsLocked = defaultMenuItem.IsLocked;
            if (existingMenuItemOrNull.IsLocked)
            {
                existingMenuItemOrNull.AccessLevel = defaultMenuItem.AccessLevel;
                existingMenuItemOrNull.IsActive = true;
            }

            if (shouldUpgradeDefaultMenuAccessLevel(existingMenuItemOrNull, defaultMenuItem))
            {
                existingMenuItemOrNull.AccessLevel = defaultMenuItem.AccessLevel;
            }

            if (shouldApplyCurrentDefaultMenuAccessLevel(existingMenuItemOrNull))
            {
                existingMenuItemOrNull.AccessLevel = defaultMenuItem.AccessLevel;
            }

            upgradeStoredDefaultMenuSortOrder(existingMenuItemOrNull, defaultMenuItem);
            upgradeStoredDefaultMenuDisplayName(existingMenuItemOrNull, defaultMenuItem);
        }
    }

    private List<NavigationMenuItem> createDefaultMenuItems()
    {
        List<NavigationMenuItem> menuItems = new List<NavigationMenuItem>();
        menuItems.Add(createMenuItem("Dashboard", "홈", "/dashboard", "Dashboard", 10, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Calendar", "일정", "/calendar", "CalendarMonth", 20, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Todos", "할 일", "/todos", "TaskAlt", 30, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("WorkGuidelines", "업무 기준", "/work-guidelines", "Rule", 40, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("WorkResources", OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, "/work-resources", "EventAvailable", 50, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Materials", "팀 자료", "/materials", "Folder", 100, EMenuAccessLevel.TeamMember, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ResponsibleResources", OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, "/responsible-resources", "SupervisorAccount", 110, EMenuAccessLevel.TeamLeader, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ManagementFiles", OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME, "/management-files", "Business", 120, EMenuAccessLevel.Management, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Common", OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, "/common", "Groups", 130, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Public", OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, "/public-files", "Public", 140, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("Upload", "파일 올리기", "/upload", "UploadFile", 150, EMenuAccessLevel.TeamMember, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ApprovalRequest", "결재 요청", "/approvals/request", "Approval", 200, EMenuAccessLevel.TeamMember, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ApprovalInbox", "내 결재함", "/approvals/inbox", "FactCheck", 210, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ApprovalHistory", "결재 기록", "/approvals/history", "HistoryEdu", 220, EMenuAccessLevel.Authenticated, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("ApprovedFiles", "결재 완료 자료", "/approved-files", "Verified", 230, EMenuAccessLevel.TeamMember, EActivationState.Active, EMenuTeamVisibility.AllTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("AdminUsers", "사용자 관리", "/admin/users", "ManageAccounts", 300, EMenuAccessLevel.ManagementManager, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("AdminTeams", "팀 관리", "/admin/teams", "Groups2", 310, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("AdminPositions", "직책과 권한 설정", "/admin/positions", "AdminPanelSettings", 320, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Locked));
        menuItems.Add(createMenuItem("AdminApprovalWorkflows", "결재 경로 설정", "/admin/approval-workflows", "AccountTree", 330, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Locked));
        menuItems.Add(createMenuItem("AdminSettings", "기본 설정", "/admin/settings", "Settings", 340, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Editable));
        menuItems.Add(createMenuItem("AdminMenus", "메뉴 보이기 설정", "/admin/menus", "Menu", 350, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Locked));
        menuItems.Add(createMenuItem("AdminGoogle", "Google 연결 설정", "/admin/google", "CloudSync", 360, EMenuAccessLevel.Admin, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Locked));
        menuItems.Add(createMenuItem("AdminCalendarAudit", "일정 삭제 기록", "/admin/calendar-audit", "History", 370, EMenuAccessLevel.ManagementManager, EActivationState.Active, EMenuTeamVisibility.SelectedTeams, ENavigationMenuLockState.Editable));
        return menuItems;
    }

    private bool shouldUpgradeDefaultMenuAccessLevel(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
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

    private bool shouldApplyCurrentDefaultMenuAccessLevel(NavigationMenuItem existingMenuItem)
    {
        return existingMenuItem.MenuKey == "AdminUsers" || existingMenuItem.MenuKey == "AdminTeams" || existingMenuItem.MenuKey == "AdminPositions" || existingMenuItem.MenuKey == "AdminApprovalWorkflows" || existingMenuItem.MenuKey == "AdminSettings" || existingMenuItem.MenuKey == "AdminCalendarAudit";
    }

    private void deactivateRemovedDefaultMenus(IReadOnlyList<NavigationMenuItem> existingMenuItems)
    {
        foreach (NavigationMenuItem existingMenuItem in existingMenuItems)
        {
            if (NavigationMenuRetirementPolicy.IsRetired(existingMenuItem.MenuKey, existingMenuItem.Href))
            {
                existingMenuItem.IsActive = false;
            }
        }
    }

    private void upgradeStoredDefaultMenuSortOrder(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        int previousDefaultSortOrder = getPreviousDefaultMenuSortOrder(existingMenuItem.MenuKey);
        if (previousDefaultSortOrder >= 0 && existingMenuItem.SortOrder == previousDefaultSortOrder)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "ResponsibleResources" && existingMenuItem.SortOrder == 120)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkGuidelines" && existingMenuItem.SortOrder == 130)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkResources" && existingMenuItem.SortOrder == 140)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "Materials" && existingMenuItem.SortOrder == 30)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "Calendar" && existingMenuItem.SortOrder == 70)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "ResponsibleResources" && existingMenuItem.SortOrder == 80)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkGuidelines" && existingMenuItem.SortOrder == 80)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkGuidelines" && existingMenuItem.SortOrder == 90)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkResources" && existingMenuItem.SortOrder == 90)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "WorkResources" && existingMenuItem.SortOrder == 100)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "Archive" && existingMenuItem.SortOrder == 100)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "Archive" && existingMenuItem.SortOrder == 110)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
            return;
        }

        if (existingMenuItem.MenuKey == "AdminMenus" && existingMenuItem.SortOrder == 240)
        {
            existingMenuItem.SortOrder = defaultMenuItem.SortOrder;
        }
    }

    private int getPreviousDefaultMenuSortOrder(string menuKey)
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

    private void upgradeStoredDefaultMenuDisplayName(NavigationMenuItem existingMenuItem, NavigationMenuItem defaultMenuItem)
    {
        if (isPreviousDefaultMenuDisplayName(existingMenuItem.MenuKey, existingMenuItem.DisplayName))
        {
            existingMenuItem.DisplayName = defaultMenuItem.DisplayName;
        }
    }

    private bool isPreviousDefaultMenuDisplayName(string menuKey, string displayName)
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

    private NavigationMenuItem createMenuItem(string menuKey, string displayName, string href, string iconKey, int sortOrder, EMenuAccessLevel accessLevel, EActivationState activationState, EMenuTeamVisibility menuTeamVisibility, ENavigationMenuLockState lockState)
    {
        NavigationMenuItem menuItem = new NavigationMenuItem();
        menuItem.MenuKey = menuKey;
        menuItem.DisplayName = displayName;
        menuItem.Href = href;
        menuItem.IconKey = iconKey;
        menuItem.SortOrder = sortOrder;
        menuItem.AccessLevel = accessLevel;
        menuItem.IsActive = activationState == EActivationState.Active;
        menuItem.IsVisibleToAllTeams = menuTeamVisibility == EMenuTeamVisibility.AllTeams;
        menuItem.IsLocked = lockState == ENavigationMenuLockState.Locked;
        return menuItem;
    }
}
