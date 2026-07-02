using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    [SupplyParameterFromQuery(Name = "message")]
    public string? MessageOrNull
    {
        get; set;
    }

    private GoogleConnectionStatus mStatus = new GoogleConnectionStatus();
    private List<GoogleDriveFolderItem> mFolders = new List<GoogleDriveFolderItem>();
    private List<GoogleCalendarListItem> mCalendars = new List<GoogleCalendarListItem>();
    private List<GoogleTaskListItem> mTaskLists = new List<GoogleTaskListItem>();
    private List<GoogleFolderAssignmentOption> mFolderAssignmentOptions = new List<GoogleFolderAssignmentOption>();
    private List<GoogleCalendarAssignmentOption> mCalendarAssignmentOptions = new List<GoogleCalendarAssignmentOption>();
    private List<GoogleTaskListAssignmentOption> mTaskListAssignmentOptions = new List<GoogleTaskListAssignmentOption>();
    private List<GoogleDriveFolderPathItem> mFolderPath = new List<GoogleDriveFolderPathItem>();
    private List<GoogleHealthCheckItem> mHealthItems = new List<GoogleHealthCheckItem>();
    private GoogleWorkspaceAssignmentTargetKey mSelectedFolderTargetKey;
    private GoogleWorkspaceAssignmentTargetKey mSelectedCalendarTargetKey;
    private GoogleWorkspaceAssignmentTargetKey mSelectedTaskListTargetKey;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mShouldShowHealthyItems;
    private bool mIsEnsuringStandardDriveFolders;
    private bool mIsEnsuringStandardCalendars;
    private bool mIsApplyingStandardDriveFolderNames;
    private bool mIsEnsuringStandardTaskLists;

    protected override async Task OnInitializedAsync()
    {
        resetFolderPath();
        await loadStatusAsync();
        await loadAssignmentOptionsAsync();
        if (mStatus.IsConnected)
        {
            Task healthCheckTask = loadHealthCheckAsync();
            Task currentFolderTask = loadCurrentFolderAsync();
            Task calendarsTask = loadCalendarsAsync();
            Task taskListsTask = loadTaskListsAsync();
            await Task.WhenAll(healthCheckTask, currentFolderTask, calendarsTask, taskListsTask);
            return;
        }

        await loadHealthCheckAsync();
    }

    private async Task loadStatusAsync()
    {
        mStatus = await GoogleConnectionService.GetStatusAsync(CancellationToken.None);
    }

    private async Task loadAssignmentOptionsAsync()
    {
        IReadOnlyList<GoogleFolderAssignmentOption> folderOptions = await GoogleWorkspaceSetupService.GetFolderAssignmentOptionsAsync(CancellationToken.None);
        mFolderAssignmentOptions = folderOptions.ToList();
        if (mFolderAssignmentOptions.Count > 0)
        {
            mSelectedFolderTargetKey = mFolderAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> calendarOptions = await GoogleWorkspaceSetupService.GetCalendarAssignmentOptionsAsync(CancellationToken.None);
        mCalendarAssignmentOptions = calendarOptions.ToList();
        if (mCalendarAssignmentOptions.Count > 0)
        {
            mSelectedCalendarTargetKey = mCalendarAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> taskListOptions = await GoogleWorkspaceSetupService.GetTaskListAssignmentOptionsAsync(CancellationToken.None);
        mTaskListAssignmentOptions = taskListOptions.ToList();
        if (mTaskListAssignmentOptions.Count > 0)
        {
            mSelectedTaskListTargetKey = mTaskListAssignmentOptions[0].TargetKey;
        }
    }

    private async Task disconnectGoogleAsync()
    {
        ServiceResult disconnectResult = await GoogleConnectionService.DisconnectAsync(CancellationToken.None);
        showOperationResult(disconnectResult, "Google 계정 연결이 해제되었습니다.");
        await loadStatusAsync();
        mFolders = new List<GoogleDriveFolderItem>();
        mCalendars = new List<GoogleCalendarListItem>();
        mTaskLists = new List<GoogleTaskListItem>();
        await loadHealthCheckAsync();
    }
}
