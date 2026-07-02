using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private List<TodoTaskListReference> mTaskLists = new List<TodoTaskListReference>();
    private HashSet<string> mWritableTaskListIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private List<TodoTaskItem> mTasks = new List<TodoTaskItem>();
    private string mSelectedTaskListId = string.Empty;
    private string mNewTaskTitle = string.Empty;
    private string mEditingTaskId = string.Empty;
    private string mEditingTitle = string.Empty;
    private DateTime? mNewTaskDueAt;
    private DateTime? mEditingDueAt;
    private string mDeletingTaskId = string.Empty;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private ElementReference mRefreshScopeElement;
    private ElementReference mTodoBoardElement;
    private ChangeVersionSnapshot mLoadedChangeSnapshot = new ChangeVersionSnapshot();
    private TimeZoneInfo mClientTimeZoneInfo = TimeZoneInfo.Local;
    private IJSObjectReference? mTodoModuleOrNull;
    private IJSObjectReference? mRefreshModuleOrNull;
    private DotNetObjectReference<Todos>? mDotNetReferenceOrNull;
    private ETodoTaskSortMode mTaskSortMode = ETodoTaskSortMode.DueDateAscending;
    private bool mIsLoadingLists = true;
    private bool mIsLoadingTasks;
    private bool mIsSaving;
    private bool mShouldInitializeSortable;
    private bool mShouldShowTodoAuthor = true;
    private bool mHasExternalChanges;
    private bool mIsNewTaskDuePickerOpen;
    private bool mIsEditingDuePickerOpen;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        GlobalSettingFormModel globalSetting = await GlobalSettingService.GetGlobalSettingAsync(CancellationToken.None);
        mShouldShowTodoAuthor = globalSetting.ShouldShowTodoAuthor;
        await loadTaskListsAsync();
    }
}
