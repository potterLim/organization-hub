using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private const string ALL_PERMISSION_CATEGORY = "전체";

    private List<Team> mActiveTeams = new List<Team>();
    private List<OrganizationPosition> mPositions = new List<OrganizationPosition>();
    private List<PermissionDefinition> mPermissionDefinitions = new List<PermissionDefinition>();
    private PositionFormModel mNewPositionFormModel = new PositionFormModel();
    private PositionFormModel mPermissionFormModel = new PositionFormModel();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<AdminPositions>? mDotNetReferenceOrNull;
    private int mSelectedPermissionTeamId;
    private int? mSelectedPositionIdOrNull;
    private string mSelectedPermissionCategory = string.Empty;
    private bool mShowOnlyAllowedPermissions;
    private bool mShouldInitializeSortable = true;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        ensurePermissionCategorySelection();
        mSelectedPermissionTeamId = getDefaultTeamId();
        mNewPositionFormModel = createEmptyFormModel(mSelectedPermissionTeamId);
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }
}
