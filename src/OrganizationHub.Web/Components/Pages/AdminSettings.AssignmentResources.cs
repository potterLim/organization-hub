using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private string getFolderAssignmentId(GoogleFolderAssignmentOption option)
    {
        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.OfficialRoot)
        {
            return mFormModel.DriveResources.OfficialRootFolderId;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Public)
        {
            return mFormModel.DriveResources.PublicFolderId;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Common)
        {
            return mFormModel.DriveResources.CommonFolderId;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.ResponsibleResources)
        {
            return mFormModel.DriveResources.ResponsibleResourcesFolderId;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.WorkResources)
        {
            return mFormModel.DriveResources.WorkResourcesFolderId;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamIdOrNull);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.TeamMaterials)
        {
            return getTextOrEmpty(teamOrNull.MaterialsFolderId);
        }

        return getTextOrEmpty(teamOrNull.ApprovedFolderId);
    }

    private string getCalendarAssignmentId(GoogleCalendarAssignmentOption option)
    {
        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
        {
            return mFormModel.CalendarResources.OrganizationCalendarId;
        }

        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Responsible)
        {
            return mFormModel.CalendarResources.ResponsibleCalendarId;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamIdOrNull);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getTextOrEmpty(teamOrNull.CalendarId);
    }

    private string getTaskListAssignmentId(GoogleTaskListAssignmentOption option)
    {
        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
        {
            return mFormModel.TaskResources.OrganizationTaskListId;
        }

        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Responsible)
        {
            return mFormModel.TaskResources.ResponsibleTaskListId;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamIdOrNull);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getTextOrEmpty(teamOrNull.TaskListId);
    }

    private Team? getTeamOrNull(OrganizationTeamId? teamIdOrNull)
    {
        int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
        if (teamIdValueOrNull.HasValue == false)
        {
            return null;
        }

        int teamId = teamIdValueOrNull.GetValueOrDefault();
        return mTeams.FirstOrDefault(team => team.Id == teamId);
    }
}
