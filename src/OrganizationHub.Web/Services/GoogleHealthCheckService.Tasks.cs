using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
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
        addTaskListItem(items, "Google Tasks", "조직 공용 Google Tasks 목록", setting.OrganizationTaskListId, taskListIds);
        addTaskListItem(items, "Google Tasks", "책임자 Google Tasks 목록", setting.ResponsibleTaskListId, taskListIds);

        foreach (Team team in teams)
        {
            addTaskListItem(
                items,
                "팀 Google Tasks",
                team.DisplayName + " Google Tasks 목록",
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
            addItem(items, area, name, STATUS_WARNING, "Google Tasks 목록이 연결되지 않았습니다.");
            return;
        }

        bool hasTaskList = taskListIds.Contains(taskListIdOrNull);
        addItem(
            items,
            area,
            name,
            hasTaskList ? STATUS_HEALTHY : STATUS_ERROR,
            hasTaskList ? "연결된 계정에서 확인되었습니다." : "연결된 계정에서 이 Google Tasks 목록을 찾을 수 없습니다.");
    }
}
