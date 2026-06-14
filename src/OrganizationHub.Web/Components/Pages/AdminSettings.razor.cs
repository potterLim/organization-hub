using System.Collections.Generic;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private const string DRIVE_ROOT_ID = "root";

    private GlobalSettingFormModel mFormModel = new GlobalSettingFormModel();
    private GoogleConnectionStatus mStatus = new GoogleConnectionStatus();
    private List<Team> mTeams = new List<Team>();
    private List<GoogleFolderAssignmentOption> mFolderAssignmentOptions = new List<GoogleFolderAssignmentOption>();
    private List<GoogleCalendarAssignmentOption> mCalendarAssignmentOptions = new List<GoogleCalendarAssignmentOption>();
    private List<GoogleTaskListAssignmentOption> mTaskListAssignmentOptions = new List<GoogleTaskListAssignmentOption>();
    private List<GoogleDriveFolderPathItem> mFolderPath = new List<GoogleDriveFolderPathItem>();
    private List<DriveFileItem> mDriveItems = new List<DriveFileItem>();
    private List<GoogleCalendarListItem> mCalendars = new List<GoogleCalendarListItem>();
    private List<GoogleTaskListItem> mTaskLists = new List<GoogleTaskListItem>();
    private Dictionary<string, string> mDriveResourceNameById = new Dictionary<string, string>();
    private GoogleWorkspaceAssignmentTargetKey mSelectedFolderTargetKey;
    private GoogleWorkspaceAssignmentTargetKey mSelectedCalendarTargetKey;
    private GoogleWorkspaceAssignmentTargetKey mSelectedTaskListTargetKey;
    private ESettingFileAssignmentTarget mSelectedFileAssignmentTarget = ESettingFileAssignmentTarget.NoticeFile;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoadingDriveItems;
    private bool mIsRefreshing;

    protected override async Task OnInitializedAsync()
    {
        await loadConfigurationAsync();
        await loadConnectionStatusAsync();
        await loadAssignmentOptionsAsync();
        resetDrivePathToOfficialRoot();
        if (mStatus.IsConnected)
        {
            Task driveItemsTask = loadDriveItemsAsync();
            Task calendarsTask = loadCalendarsAsync();
            Task taskListsTask = loadTaskListsAsync();
            Task driveResourceNamesTask = loadDriveResourceNamesAsync();
            await Task.WhenAll(driveItemsTask, calendarsTask, taskListsTask, driveResourceNamesTask);
        }
    }


}
