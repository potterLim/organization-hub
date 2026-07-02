using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService
{
    private async Task addTaskListItemsAsync(
        List<GoogleHealthCheckItem> items,
        GlobalSetting setting,
        List<Team> teams,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleTaskListItem> taskLists =
            await mGoogleWorkspaceBrowserService.ListTaskListsAsync(cancellationToken);
        HashSet<string> taskListIds = new HashSet<string>(taskLists.Select(taskList => taskList.Id));
        addTaskListItem(items, "Google Tasks", OrganizationHubWorkspaceDefaults.ORGANIZATION_TASK_LIST_NAME, setting.OrganizationTaskListId, taskListIds);
        addTaskListItem(items, "Google Tasks", OrganizationHubWorkspaceDefaults.RESPONSIBLE_TASK_LIST_NAME, setting.ResponsibleTaskListId, taskListIds);

        foreach (Team team in teams)
        {
            addTaskListItem(
                items,
                "팀 할 일",
                team.DisplayName + " 할 일",
                team.TaskListId,
                taskListIds);
        }
    }

    private void addTaskListItem(
        List<GoogleHealthCheckItem> items,
        string area,
        string name,
        string? taskListIdOrNull,
        HashSet<string> taskListIds)
    {
        if (string.IsNullOrWhiteSpace(taskListIdOrNull))
        {
            addItem(items, area, name, EGoogleHealthStatus.Warning, "Google Tasks 목록이 연결되지 않았습니다.");
            return;
        }

        bool hasTaskList = taskListIds.Contains(taskListIdOrNull);
        addItem(
            items,
            area,
            name,
            hasTaskList ? EGoogleHealthStatus.Healthy : EGoogleHealthStatus.Error,
            hasTaskList ? "연결된 계정에서 확인되었습니다." : "연결된 계정에서 이 Google Tasks 목록을 찾을 수 없습니다.");
    }
}
