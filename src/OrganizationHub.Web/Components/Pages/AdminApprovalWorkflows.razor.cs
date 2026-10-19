using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private List<Team> mTeams = new List<Team>();
    private List<OrganizationPosition> mPositions = new List<OrganizationPosition>();
    private List<UserAccount> mUsers = new List<UserAccount>();
    private List<PermissionDefinition> mPermissionDefinitions = new List<PermissionDefinition>();
    private List<ApprovalWorkflow> mWorkflows = new List<ApprovalWorkflow>();
    private ApprovalWorkflowFormModel mFormModel = new ApprovalWorkflowFormModel();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<AdminApprovalWorkflows>? mDotNetReferenceOrNull;
    private bool mShouldInitializeSortable = true;
    private string mFormTitle = "새 결재 경로";
    private string mMessage = string.Empty;
    private bool mHasSaveConflict;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        clearForm();
    }
}
