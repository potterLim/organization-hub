using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    private async Task ensureCanWriteTaskListAsync(UserSession userSession, TodoTaskListId taskListId, CancellationToken cancellationToken)
    {
        if (taskListId.IsAggregate)
        {
            throw new InvalidOperationException("실제 할 일 목록을 선택해 주세요.");
        }

        if (taskListId.HasValue == false)
        {
            throw new InvalidOperationException("할 일 목록을 선택해 주세요.");
        }

        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Writable, cancellationToken);
        bool canUseTaskList = taskLists.Any(taskList => taskList.TaskListId == taskListId);
        if (canUseTaskList == false)
        {
            throw new UnauthorizedAccessException("이 할 일 목록을 수정할 권한이 없습니다.");
        }
    }

    private async Task<TodoTaskListReference> getAllowedTaskListAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        ETodoTaskListAccessMode accessMode,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, accessMode, cancellationToken);
        TodoTaskListReference? taskListOrNull = taskLists.FirstOrDefault(taskList => taskList.TaskListId == taskListId);
        if (taskListOrNull == null)
        {
            throw new UnauthorizedAccessException("이 할 일 목록에 접근할 권한이 없습니다.");
        }

        return taskListOrNull;
    }

    private void validateTaskForm(TodoTaskFormModel formModel)
    {
        if (formModel.TaskListIdValue.HasValue == false)
        {
            throw new InvalidOperationException("할 일 목록을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.Title))
        {
            throw new InvalidOperationException("할 일을 입력해 주세요.");
        }
    }

    private void validateTaskId(TodoTaskId taskId)
    {
        if (taskId.HasValue == false)
        {
            throw new InvalidOperationException("할 일 항목을 찾을 수 없습니다.");
        }
    }
}
