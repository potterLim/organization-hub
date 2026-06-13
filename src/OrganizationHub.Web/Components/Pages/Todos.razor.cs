using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private List<TodoTaskListReference> mTaskLists = new List<TodoTaskListReference>();
    private HashSet<string> mWritableTaskListIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private List<TodoTaskItem> mTasks = new List<TodoTaskItem>();
    private string mSelectedTaskListID = string.Empty;
    private string mNewTaskTitle = string.Empty;
    private string mEditingTaskID = string.Empty;
    private string mEditingTitle = string.Empty;
    private string mDeletingTaskID = string.Empty;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private ElementReference mRefreshScopeElement;
    private ElementReference mTodoBoardElement;
    private ChangeVersionSnapshot mLoadedChangeSnapshot = new ChangeVersionSnapshot();
    private IJSObjectReference? mTodoModuleOrNull;
    private IJSObjectReference? mRefreshModuleOrNull;
    private DotNetObjectReference<Todos>? mDotNetReferenceOrNull;
    private bool mIsLoadingLists = true;
    private bool mIsLoadingTasks;
    private bool mIsSaving;
    private bool mShouldInitializeSortable;
    private bool mShowTodoAuthor = true;
    private bool mHasExternalChanges;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        GlobalSettingFormModel globalSetting = await TeamManagementService.GetGlobalSettingAsync(CancellationToken.None);
        mShowTodoAuthor = globalSetting.ShowTodoAuthor;
        await loadTaskListsAsync();
    }
}
