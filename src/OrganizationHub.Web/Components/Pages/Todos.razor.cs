using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Domain;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await initializeChangeRefreshAsync();
        }

        if (mShouldInitializeSortable == false || mIsLoadingLists || mIsLoadingTasks)
        {
            return;
        }

        mShouldInitializeSortable = false;
        if (canSortTasks() == false)
        {
            await disposeSortableAsync();
            return;
        }

        if (mTodoModuleOrNull == null)
        {
            mTodoModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubTodoBoard.js");
        }

        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        await mTodoModuleOrNull.InvokeVoidAsync("initializeTodoSortable", mTodoBoardElement, mDotNetReferenceOrNull);
    }

    private async Task loadTaskListsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mIsLoadingLists = true;
        try
        {
            IReadOnlyList<TodoTaskListReference> taskLists = await TodoService.GetReadableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            mTaskLists = taskLists.ToList();
            mWritableTaskListIDs = writableTaskLists.Select(taskList => taskList.TaskListID).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (mTaskLists.Count > 0)
            {
                mSelectedTaskListID = getInitialTaskListID();
                await loadTasksAsync();
            }
        }
        catch (Exception exception)
        {
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingLists = false;
    }

    private async Task handleTaskListChangedAsync(string taskListID)
    {
        mSelectedTaskListID = taskListID;
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
        mDeletingTaskID = string.Empty;
        await loadTasksAsync();
    }

    private async Task loadTasksAsync()
    {
        if (CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListID))
        {
            return;
        }

        mIsLoadingTasks = true;
        mShouldInitializeSortable = false;
        bool shouldInitializeSortable = false;
        try
        {
            IReadOnlyList<string> changeVersionKeys = getChangeVersionKeys();
            ChangeVersionReadResult<IReadOnlyList<TodoTaskItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => TodoService.GetTasksAsync(CurrentUserSessionOrNull, mSelectedTaskListID, cancellationToken),
                CancellationToken.None);
            mTasks = readResult.Value.ToList();
            mMessage = string.Empty;
            mDeletingTaskID = string.Empty;
            EChangeVersionStability stability = readResult.IsStable ? EChangeVersionStability.Stable : EChangeVersionStability.Unstable;
            recordLoadedChangeSnapshot(readResult.Snapshot, stability);
            shouldInitializeSortable = true;
        }
        catch (Exception exception)
        {
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingTasks = false;
        mShouldInitializeSortable = shouldInitializeSortable;
    }

    private async Task handleNewTaskKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter")
        {
            await createTaskAsync();
        }
    }

    private async Task createTaskAsync()
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canSubmitNewTask() == false)
        {
            return;
        }

        mIsSaving = true;
        string taskTitle = mNewTaskTitle.Trim();
        TodoTaskItem optimisticTask = createOptimisticTask(taskTitle);
        try
        {
            prependTask(optimisticTask);
            mNewTaskTitle = string.Empty;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListID = mSelectedTaskListID;
            formModel.Title = taskTitle;
            TodoTaskItem createdTask = await TodoService.CreateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            removeTaskFromLocalList(optimisticTask.TaskListID, optimisticTask.ID);
            prependTask(createdTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            removeTaskFromLocalList(optimisticTask.TaskListID, optimisticTask.ID);
            if (string.IsNullOrWhiteSpace(mNewTaskTitle))
            {
                mNewTaskTitle = taskTitle;
            }

            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private void beginEdit(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mEditingTaskID = task.ID;
        mEditingTitle = task.Title;
        mDeletingTaskID = string.Empty;
    }

    private void setEditingTitle(string title)
    {
        mEditingTitle = title;
    }

    private void cancelEdit()
    {
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void requestDelete(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mDeletingTaskID = task.ID;
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void cancelDelete()
    {
        mDeletingTaskID = string.Empty;
    }

    private async Task saveEditAsync(TodoTaskItem task)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            string editedTitle = mEditingTitle.Trim();
            TodoTaskItem optimisticTask = cloneTask(task);
            optimisticTask.Title = editedTitle;
            optimisticTask.UpdatedAt = DateTimeOffset.Now;
            replaceTask(optimisticTask);
            cancelEdit();
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListID = task.TaskListID;
            formModel.TaskID = task.ID;
            formModel.Title = editedTitle;
            TodoTaskItem updatedTask = await TodoService.UpdateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            updatedTask.TaskListDisplayName = task.TaskListDisplayName;
            replaceTask(updatedTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            mEditingTaskID = task.ID;
            mEditingTitle = task.Title;
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task setTaskCompletionStateAsync(TodoTaskItem task, ETodoTaskCompletionState completionState)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            TodoTaskItem optimisticTask = cloneTask(task);
            bool isCompleted = completionState == ETodoTaskCompletionState.Completed;
            optimisticTask.IsCompleted = isCompleted;
            optimisticTask.CompletedAt = isCompleted ? DateTimeOffset.Now : null;
            optimisticTask.UpdatedAt = DateTimeOffset.Now;
            replaceTask(optimisticTask);
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskItem updatedTask = completionState == ETodoTaskCompletionState.Completed ? await TodoService.CompleteTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None) : await TodoService.ReopenTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None);
            updatedTask.TaskListDisplayName = task.TaskListDisplayName;
            replaceTask(updatedTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task deleteTaskAsync(TodoTaskItem task)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            removeTaskFromLocalList(task.TaskListID, task.ID);
            mDeletingTaskID = string.Empty;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            await TodoService.DeleteTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task moveTaskBeforeAsync(string sourceTaskID, string targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        int sourceIndex = pendingTasks.FindIndex(task => task.ID == sourceTaskID);
        int targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        TodoTaskItem sourceTask = pendingTasks[sourceIndex];
        if (targetIndex == sourceIndex || targetIndex == sourceIndex + 1)
        {
            return;
        }

        pendingTasks.RemoveAt(sourceIndex);
        targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID);
        if (targetIndex < 0)
        {
            return;
        }

        string previousTaskID = targetIndex <= 0 ? string.Empty : pendingTasks[targetIndex - 1].ID;
        await moveTaskAsync(sourceTask, previousTaskID);
    }

    private async Task moveTaskAfterAsync(string sourceTaskID, string targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        TodoTaskItem? sourceTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == sourceTaskID);
        TodoTaskItem? targetTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == targetTaskID);
        if (sourceTaskOrNull == null || targetTaskOrNull == null)
        {
            return;
        }

        int sourceIndex = pendingTasks.IndexOf(sourceTaskOrNull);
        int targetIndex = pendingTasks.IndexOf(targetTaskOrNull);
        if (targetIndex == sourceIndex || targetIndex == sourceIndex - 1)
        {
            return;
        }

        await moveTaskAsync(sourceTaskOrNull, targetTaskOrNull.ID);
    }

    private async Task moveTaskAsync(TodoTaskItem task, string previousTaskID)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            moveTaskInLocalList(task, previousTaskID);
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskItem movedTask = await TodoService.MoveTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, previousTaskID, CancellationToken.None);
            movedTask.TaskListDisplayName = task.TaskListDisplayName;
            moveTaskInLocalList(movedTask, previousTaskID);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    [JSInvokable("MoveTaskBeforeFromScript")]
    public async Task MoveTaskBeforeFromScriptAsync(string sourceTaskID, string targetTaskID)
    {
        await moveTaskBeforeAsync(sourceTaskID, targetTaskID);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveTaskAfterFromScript")]
    public async Task MoveTaskAfterFromScriptAsync(string sourceTaskID, string targetTaskID)
    {
        await moveTaskAfterAsync(sourceTaskID, targetTaskID);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("CheckForExternalChanges")]
    public async Task CheckForExternalChangesAsync()
    {
        if (mIsLoadingLists || mIsLoadingTasks || CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListID))
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        if (mIsSaving || string.IsNullOrWhiteSpace(mEditingTaskID) == false || string.IsNullOrWhiteSpace(mDeletingTaskID) == false)
        {
            mHasExternalChanges = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await loadTasksAsync();
        await InvokeAsync(StateHasChanged);
    }

    private List<TodoTaskItem> getPendingTasks()
    {
        return mTasks.Where(task => task.IsCompleted == false).ToList();
    }

    private List<TodoTaskItem> getCompletedTasks()
    {
        return mTasks.Where(task => task.IsCompleted).OrderByDescending(task => task.CompletedAt).ThenBy(task => task.Title).ToList();
    }

    private List<TodoTaskItem> cloneTasks(List<TodoTaskItem> tasks)
    {
        return tasks.Select(task => cloneTask(task)).ToList();
    }

    private TodoTaskItem cloneTask(TodoTaskItem task)
    {
        TodoTaskItem clonedTask = new TodoTaskItem();
        clonedTask.ID = task.ID;
        clonedTask.TaskListID = task.TaskListID;
        clonedTask.TaskListDisplayName = task.TaskListDisplayName;
        clonedTask.Title = task.Title;
        clonedTask.Position = task.Position;
        clonedTask.CreatedByDisplayName = task.CreatedByDisplayName;
        clonedTask.IsCompleted = task.IsCompleted;
        clonedTask.CreatedAt = task.CreatedAt;
        clonedTask.UpdatedAt = task.UpdatedAt;
        clonedTask.CompletedAt = task.CompletedAt;
        return clonedTask;
    }

    private TodoTaskItem createOptimisticTask(string taskTitle)
    {
        TodoTaskItem optimisticTask = new TodoTaskItem();
        optimisticTask.ID = "optimistic-" + Guid.NewGuid().ToString("N");
        optimisticTask.TaskListID = mSelectedTaskListID;
        optimisticTask.TaskListDisplayName = getSelectedTaskListDisplayName();
        optimisticTask.Title = taskTitle;
        optimisticTask.Position = string.Empty;
        optimisticTask.CreatedByDisplayName = getCurrentUserDisplayName();
        optimisticTask.IsCompleted = false;
        optimisticTask.CreatedAt = DateTime.Now;
        optimisticTask.UpdatedAt = DateTimeOffset.Now;
        optimisticTask.CompletedAt = null;
        return optimisticTask;
    }

    private void prependTask(TodoTaskItem task)
    {
        removeTaskFromLocalList(task.TaskListID, task.ID);
        mTasks.Insert(0, task);
    }

    private void replaceTask(TodoTaskItem task)
    {
        int taskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListID == task.TaskListID && existingTask.ID == task.ID);
        if (taskIndex < 0)
        {
            prependTask(task);
            return;
        }

        mTasks[taskIndex] = task;
    }

    private void removeTaskFromLocalList(string taskListID, string taskID)
    {
        mTasks.RemoveAll(existingTask => existingTask.TaskListID == taskListID && existingTask.ID == taskID);
    }

    private void moveTaskInLocalList(TodoTaskItem movedTask, string previousTaskID)
    {
        removeTaskFromLocalList(movedTask.TaskListID, movedTask.ID);
        if (string.IsNullOrWhiteSpace(previousTaskID))
        {
            int firstPendingTaskIndex = mTasks.FindIndex(existingTask => existingTask.IsCompleted == false);
            if (firstPendingTaskIndex < 0)
            {
                mTasks.Insert(0, movedTask);
                return;
            }

            mTasks.Insert(firstPendingTaskIndex, movedTask);
            return;
        }

        int previousTaskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListID == movedTask.TaskListID && existingTask.ID == previousTaskID);
        if (previousTaskIndex < 0)
        {
            mTasks.Insert(0, movedTask);
            return;
        }

        mTasks.Insert(previousTaskIndex + 1, movedTask);
    }

    private string formatTaskMeta(TodoTaskItem task)
    {
        List<string> metadataParts = new List<string>();
        if (isAggregateSelected() && string.IsNullOrWhiteSpace(task.TaskListDisplayName) == false)
        {
            metadataParts.Add(task.TaskListDisplayName);
        }

        if (mShowTodoAuthor && string.IsNullOrWhiteSpace(task.CreatedByDisplayName) == false)
        {
            metadataParts.Add("작성자: " + task.CreatedByDisplayName);
        }

        if (task.CreatedAt.HasValue)
        {
            metadataParts.Add("작성일: " + formatTaskDateTime(task.CreatedAt.Value));
        }

        if (task.IsCompleted && task.CompletedAt.HasValue)
        {
            metadataParts.Add("완료일: " + formatTaskDateTime(task.CompletedAt.Value));
        }

        return string.Join(" · ", metadataParts);
    }

    private bool isAggregateSelected()
    {
        TodoTaskListReference? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.TaskListID == mSelectedTaskListID);
        return taskListOrNull != null && taskListOrNull.IsAggregate;
    }

    private void recordLoadedChangeSnapshot(ChangeVersionSnapshot? loadedSnapshotOrNull = null, EChangeVersionStability stability = EChangeVersionStability.Stable)
    {
        if (loadedSnapshotOrNull == null)
        {
            mLoadedChangeSnapshot = ChangeVersionService.GetSnapshot(getChangeVersionKeys());
        }
        else
        {
            mLoadedChangeSnapshot = loadedSnapshotOrNull;
        }

        mHasExternalChanges = stability == EChangeVersionStability.Unstable;
    }

    private IReadOnlyList<string> getChangeVersionKeys()
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.TODO);
        if (TodoTaskListReference.IsAggregateTaskListID(mSelectedTaskListID) == false)
        {
            keys.Add(ChangeVersionKeys.CreateTodoTaskListKey(mSelectedTaskListID));
        }

        return keys;
    }

    private string getInitialTaskListID()
    {
        if (mTaskLists.Count == 2 && mTaskLists[0].IsAggregate)
        {
            return mTaskLists[1].TaskListID;
        }

        return mTaskLists[0].TaskListID;
    }

    private bool canCreateTask()
    {
        return string.IsNullOrWhiteSpace(mSelectedTaskListID) == false && isAggregateSelected() == false && isSelectedTaskListWritable();
    }

    private bool canSubmitNewTask()
    {
        return canCreateTask() && string.IsNullOrWhiteSpace(mNewTaskTitle) == false;
    }

    private bool canSortTasks()
    {
        return canCreateTask() && getPendingTasks().Count > 1;
    }

    private bool canDragTask(ETodoTaskCompletionState completionState)
    {
        return completionState != ETodoTaskCompletionState.Completed && canSortTasks();
    }

    private string getNewTaskPlaceholder()
    {
        return canCreateTask() ? "새 할 일" : "목록 선택 후 추가";
    }

    private bool isSelectedTaskListWritable()
    {
        return mWritableTaskListIDs.Contains(mSelectedTaskListID);
    }

    private bool canWriteTask(TodoTaskItem task)
    {
        return string.IsNullOrWhiteSpace(task.TaskListID) == false && mWritableTaskListIDs.Contains(task.TaskListID);
    }

    private string getSelectedTaskListDisplayName()
    {
        TodoTaskListReference? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.TaskListID == mSelectedTaskListID);
        if (taskListOrNull == null)
        {
            return string.Empty;
        }

        return taskListOrNull.DisplayName;
    }

    private string getCurrentUserDisplayName()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return string.Empty;
        }

        return CurrentUserSessionOrNull.DisplayName;
    }

    private string formatTaskDateTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatTaskDateTime(DateTimeOffset dateTime)
    {
        return dateTime.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private void showError(string message)
    {
        mMessage = message;
        mMessageSeverity = Severity.Warning;
    }

    private string createTodoErrorMessage(Exception exception)
    {
        return "할 일 작업을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
    }

    private async ValueTask disposeSortableAsync()
    {
        if (mTodoModuleOrNull == null)
        {
            return;
        }

        await mTodoModuleOrNull.InvokeVoidAsync("disposeTodoSortable", mTodoBoardElement);
    }

    private async Task initializeChangeRefreshAsync()
    {
        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        mRefreshModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubChangeRefresh.js");
        await mRefreshModuleOrNull.InvokeVoidAsync("initializeChangeRefresh", mRefreshScopeElement, mDotNetReferenceOrNull);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mRefreshModuleOrNull != null)
            {
                await mRefreshModuleOrNull.InvokeVoidAsync("disposeChangeRefresh", mRefreshScopeElement);
                await mRefreshModuleOrNull.DisposeAsync();
            }

            if (mTodoModuleOrNull != null)
            {
                await mTodoModuleOrNull.InvokeVoidAsync("disposeTodoSortable", mTodoBoardElement);
                await mTodoModuleOrNull.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (mDotNetReferenceOrNull != null)
        {
            mDotNetReferenceOrNull.Dispose();
        }
    }
}
