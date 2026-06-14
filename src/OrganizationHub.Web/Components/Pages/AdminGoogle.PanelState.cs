using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private AdminGoogleDrivePanelState createDrivePanelState()
    {
        AdminGoogleDrivePanelState state = new AdminGoogleDrivePanelState();
        state.Status = mStatus;
        state.FolderAssignmentOptions = mFolderAssignmentOptions;
        state.SelectedFolderTargetKey = mSelectedFolderTargetKey;
        state.FolderPath = mFolderPath;
        state.Folders = mFolders;
        state.IsEnsuringStandardFolders = mIsEnsuringStandardDriveFolders;
        state.IsApplyingStandardFolderNames = mIsApplyingStandardDriveFolderNames;
        state.FormatDate = formatDate;
        return state;
    }

    private AdminGoogleDrivePanelActions createDrivePanelActions()
    {
        AdminGoogleDrivePanelActions actions = new AdminGoogleDrivePanelActions();
        actions.SelectedFolderTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setFolderTarget);
        actions.EnsureStandardFoldersRequested = EventCallback.Factory.Create(this, ensureStandardDriveFoldersAsync);
        actions.ApplyStandardFolderNamesRequested = EventCallback.Factory.Create(this, applyStandardDriveFolderNamesAsync);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadCurrentFolderAsync);
        actions.OpenPathRequested = EventCallback.Factory.Create<int>(this, openPathAsync);
        actions.OpenFolderRequested = EventCallback.Factory.Create<GoogleDriveFolderItem>(this, openFolderAsync);
        actions.AssignFolderRequested = EventCallback.Factory.Create<GoogleDriveFolderItem>(this, assignFolderAsync);
        return actions;
    }

    private AdminGoogleCalendarPanelState createCalendarPanelState()
    {
        AdminGoogleCalendarPanelState state = new AdminGoogleCalendarPanelState();
        state.Status = mStatus;
        state.CalendarAssignmentOptions = mCalendarAssignmentOptions;
        state.SelectedCalendarTargetKey = mSelectedCalendarTargetKey;
        state.Calendars = mCalendars;
        state.IsEnsuringStandardCalendars = mIsEnsuringStandardCalendars;
        state.GetCalendarAccessDisplayName = getCalendarAccessDisplayName;
        return state;
    }

    private AdminGoogleCalendarPanelActions createCalendarPanelActions()
    {
        AdminGoogleCalendarPanelActions actions = new AdminGoogleCalendarPanelActions();
        actions.SelectedCalendarTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setCalendarTarget);
        actions.EnsureStandardCalendarsRequested = EventCallback.Factory.Create(this, ensureStandardCalendarsAsync);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadCalendarsAsync);
        actions.AssignCalendarRequested = EventCallback.Factory.Create<GoogleCalendarListItem>(this, assignCalendarAsync);
        return actions;
    }

    private AdminGoogleTaskListPanelState createTaskListPanelState()
    {
        AdminGoogleTaskListPanelState state = new AdminGoogleTaskListPanelState();
        state.Status = mStatus;
        state.TaskListAssignmentOptions = mTaskListAssignmentOptions;
        state.SelectedTaskListTargetKey = mSelectedTaskListTargetKey;
        state.TaskLists = mTaskLists;
        state.IsEnsuringStandardTaskLists = mIsEnsuringStandardTaskLists;
        return state;
    }

    private AdminGoogleTaskListPanelActions createTaskListPanelActions()
    {
        AdminGoogleTaskListPanelActions actions = new AdminGoogleTaskListPanelActions();
        actions.SelectedTaskListTargetKeyChanged = EventCallback.Factory.Create<GoogleWorkspaceAssignmentTargetKey>(this, setTaskListTarget);
        actions.EnsureStandardTaskListsRequested = EventCallback.Factory.Create(this, ensureStandardTaskListsAsync);
        actions.RefreshRequested = EventCallback.Factory.Create(this, loadTaskListsAsync);
        actions.AssignTaskListRequested = EventCallback.Factory.Create<GoogleTaskListItem>(this, assignTaskListAsync);
        return actions;
    }
}
