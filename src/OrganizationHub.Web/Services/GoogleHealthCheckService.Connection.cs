using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService
{
    private void addCalendarScopeItem(
        List<GoogleHealthCheckItem> items,
        GoogleStorageConnection? connectionOrNull)
    {
        if (connectionOrNull == null)
        {
            addItem(items, "연결", "Calendar 수정 권한", EGoogleHealthStatus.Error, "활성 Google 연결을 찾을 수 없습니다.");
            return;
        }

        bool hasCalendarWriteScope = connectionOrNull.Scopes.Contains(GoogleCalendarApi.Scope.Calendar);
        addItem(
            items,
            "연결",
            "Calendar 수정 권한",
            hasCalendarWriteScope ? EGoogleHealthStatus.Healthy : EGoogleHealthStatus.Warning,
            hasCalendarWriteScope ? "일정 추가, 수정, 삭제 권한이 포함되어 있습니다." : "기존 연결은 읽기만 가능할 수 있습니다. Google 계정을 다시 연결해 주세요.");
    }

    private void addTasksScopeItem(
        List<GoogleHealthCheckItem> items,
        GoogleStorageConnection? connectionOrNull)
    {
        if (connectionOrNull == null)
        {
            addItem(items, "연결", "Google Tasks 권한", EGoogleHealthStatus.Error, "활성 Google 연결을 찾을 수 없습니다.");
            return;
        }

        bool hasTasksScope = connectionOrNull.Scopes.Contains(GoogleTasksApi.Scope.Tasks);
        addItem(
            items,
            "연결",
            "Google Tasks 권한",
            hasTasksScope ? EGoogleHealthStatus.Healthy : EGoogleHealthStatus.Warning,
            hasTasksScope ? "할 일 추가, 수정, 삭제 권한이 포함되어 있습니다." : "기존 연결에 Google Tasks 권한이 없습니다. Google 계정을 다시 연결해 주세요.");
    }
}
