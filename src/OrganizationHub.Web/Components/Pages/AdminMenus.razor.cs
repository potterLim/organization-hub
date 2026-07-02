using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private List<NavigationMenuSection> mSections = new List<NavigationMenuSection>();
    private List<NavigationMenuItem> mMenuItems = new List<NavigationMenuItem>();
    private List<Team> mTeams = new List<Team>();
    private IReadOnlyList<PermissionDefinition> mPermissionDefinitions = new List<PermissionDefinition>();
    private NavigationMenuItem? mSelectedMenuItemOrNull;
    private NavigationMenuItemFormModel mFormModel = new NavigationMenuItemFormModel();
    private NavigationMenuSectionFormModel mSectionFormModel = new NavigationMenuSectionFormModel();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<AdminMenus>? mDotNetReferenceOrNull;
    private bool mShouldInitializeSortable = true;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        if (mMenuItems.Count > 0)
        {
            editMenu(mMenuItems[0]);
        }
    }

    private async Task loadDataAsync()
    {
        IReadOnlyList<NavigationMenuSection> sections = await NavigationMenuService.GetMenuSectionsAsync(CancellationToken.None);
        IReadOnlyList<NavigationMenuItem> menuItems = await NavigationMenuService.GetManageableMenuItemsAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mPermissionDefinitions = PermissionCatalog.GetDefinitions();
        mSections = sections.ToList();
        mMenuItems = menuItems.ToList();
        mTeams = teams.Where(team => team.IsActive).ToList();
        mShouldInitializeSortable = true;
    }
}
