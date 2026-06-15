using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private List<NavigationMenuItem> mMenuItems = new List<NavigationMenuItem>();
    private List<Team> mTeams = new List<Team>();
    private NavigationMenuItem? mSelectedMenuItemOrNull;
    private NavigationMenuItemFormModel mFormModel = new NavigationMenuItemFormModel();
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
        IReadOnlyList<NavigationMenuItem> menuItems = await NavigationMenuService.GetManageableMenuItemsAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mMenuItems = menuItems.ToList();
        mTeams = teams.Where(team => team.IsActive).ToList();
        mShouldInitializeSortable = true;
    }
}
