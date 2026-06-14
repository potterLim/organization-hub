using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private AdminSettingsDrivePanelState createDrivePanelState()
    {
        AdminSettingsDrivePanelState state = new AdminSettingsDrivePanelState();
        state.Status = mStatus;
        state.HasOfficialRootFolder = hasOfficialRootFolder();
        state.FolderAssignmentOptions = mFolderAssignmentOptions;
        state.SelectedFolderTargetKey = mSelectedFolderTargetKey;
        state.SelectedFileAssignmentTarget = mSelectedFileAssignmentTarget;
        state.SelectedFolderAssignmentDisplayName = getSelectedFolderAssignmentDisplayName();
        state.SelectedFolderAssignmentDetail = getSelectedFolderAssignmentDetail();
        state.SelectedFileAssignmentDisplayName = getSelectedFileAssignmentDisplayName();
        state.SelectedFileAssignmentDetail = getSelectedFileAssignmentDetail();
        state.FolderPath = mFolderPath;
        state.DriveItems = getOrderedDriveItems();
        state.IsLoadingDriveItems = mIsLoadingDriveItems;
        state.GetFileTypeDisplayName = getFileTypeDisplayName;
        state.FormatDate = formatDate;
        return state;
    }

    private AdminSettingsDrivePanelActions createDrivePanelActions()
    {
        AdminSettingsDrivePanelActions actions = new AdminSettingsDrivePanelActions();
        actions.SelectedFolderTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setFolderTarget);
        actions.SelectedFileAssignmentTargetChanged = EventCallback.Factory.Create<ESettingFileAssignmentTarget>(this, setFileAssignmentTarget);
        actions.DriveRootRequested = EventCallback.Factory.Create(this, goToDriveRootAsync);
        actions.OfficialRootRequested = EventCallback.Factory.Create(this, goToOfficialRootAsync);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadDriveItemsAsync);
        actions.OpenPathRequested = EventCallback.Factory.Create<int>(this, openPathAsync);
        actions.OpenFolderRequested = EventCallback.Factory.Create<DriveFileItem>(this, openFolderAsync);
        actions.AssignFolderRequested = EventCallback.Factory.Create<DriveFileItem>(this, assignFolderAsync);
        actions.AssignFileRequested = EventCallback.Factory.Create<DriveFileItem>(this, assignFileAsync);
        return actions;
    }

    private AdminSettingsCalendarPanelState createCalendarPanelState()
    {
        AdminSettingsCalendarPanelState state = new AdminSettingsCalendarPanelState();
        state.Status = mStatus;
        state.CalendarAssignmentOptions = mCalendarAssignmentOptions;
        state.SelectedCalendarTargetKey = mSelectedCalendarTargetKey;
        state.SelectedCalendarAssignmentDisplayName = getSelectedCalendarAssignmentDisplayName();
        state.SelectedCalendarAssignmentDetail = getSelectedCalendarAssignmentDetail();
        state.Calendars = mCalendars;
        state.GetCalendarAccessDisplayName = getCalendarAccessDisplayName;
        return state;
    }

    private AdminSettingsCalendarPanelActions createCalendarPanelActions()
    {
        AdminSettingsCalendarPanelActions actions = new AdminSettingsCalendarPanelActions();
        actions.SelectedCalendarTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setCalendarTarget);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadCalendarsAsync);
        actions.AssignCalendarRequested = EventCallback.Factory.Create<GoogleCalendarListItem>(this, assignCalendarAsync);
        return actions;
    }

    private AdminSettingsTaskListPanelState createTaskListPanelState()
    {
        AdminSettingsTaskListPanelState state = new AdminSettingsTaskListPanelState();
        state.Status = mStatus;
        state.TaskListAssignmentOptions = mTaskListAssignmentOptions;
        state.SelectedTaskListTargetKey = mSelectedTaskListTargetKey;
        state.SelectedTaskListAssignmentDisplayName = getSelectedTaskListAssignmentDisplayName();
        state.SelectedTaskListAssignmentDetail = getSelectedTaskListAssignmentDetail();
        state.TaskLists = mTaskLists;
        return state;
    }

    private AdminSettingsTaskListPanelActions createTaskListPanelActions()
    {
        AdminSettingsTaskListPanelActions actions = new AdminSettingsTaskListPanelActions();
        actions.SelectedTaskListTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setTaskListTarget);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadTaskListsAsync);
        actions.AssignTaskListRequested = EventCallback.Factory.Create<GoogleTaskListItem>(this, assignTaskListAsync);
        return actions;
    }
}
