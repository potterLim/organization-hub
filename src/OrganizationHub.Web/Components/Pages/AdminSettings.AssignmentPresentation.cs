using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private string getSelectedFolderAssignmentDisplayName()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedFolderAssignmentDetail()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getDriveResourceName(getFolderAssignmentId(optionOrNull));
    }

    private string getSelectedFileAssignmentDisplayName()
    {
        if (mSelectedFileAssignmentTarget == ESettingFileAssignmentTarget.NoticeFile)
        {
            return "공지 파일";
        }

        return "업무 기준 파일";
    }

    private string getSelectedFileAssignmentDetail()
    {
        if (mSelectedFileAssignmentTarget == ESettingFileAssignmentTarget.NoticeFile)
        {
            return getDriveResourceName(mFormModel.FileResources.NoticeFileId);
        }

        return getDriveResourceName(mFormModel.FileResources.WorkGuidelineFileId);
    }

    private string getSelectedCalendarAssignmentDisplayName()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedCalendarAssignmentDetail()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getCalendarName(getCalendarAssignmentId(optionOrNull));
    }

    private string getSelectedTaskListAssignmentDisplayName()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedTaskListAssignmentDetail()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getTaskListName(getTaskListAssignmentId(optionOrNull));
    }

    private GoogleFolderAssignmentOption? getSelectedFolderAssignmentOptionOrNull()
    {
        return mFolderAssignmentOptions.FirstOrDefault(option => option.TargetKey.Matches(mSelectedFolderTargetKey));
    }

    private GoogleCalendarAssignmentOption? getSelectedCalendarAssignmentOptionOrNull()
    {
        return mCalendarAssignmentOptions.FirstOrDefault(option => option.TargetKey.Matches(mSelectedCalendarTargetKey));
    }

    private GoogleTaskListAssignmentOption? getSelectedTaskListAssignmentOptionOrNull()
    {
        return mTaskListAssignmentOptions.FirstOrDefault(option => option.TargetKey.Matches(mSelectedTaskListTargetKey));
    }
}
