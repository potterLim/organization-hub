using System;
using System.Globalization;
using MudBlazor;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private void showMessage(string message, Severity severity)
    {
        mMessage = message;
        mMessageSeverity = severity;
    }

    private string getCurrentFolderDisplayName()
    {
        if (mFolderPath.Count == 0)
        {
            return "선택 위치";
        }

        return mFolderPath[mFolderPath.Count - 1].Name;
    }

    private string formatDate(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatFileSize(long size)
    {
        if (size < 1024)
        {
            return size.ToString(CultureInfo.InvariantCulture) + " B";
        }

        if (size < 1024 * 1024)
        {
            return (size / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }

        return (size / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private string getFolderCountText()
    {
        return "하위 폴더 " + mFoldersInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string getSelectedFileText()
    {
        if (mSelectedFileOrNull == null)
        {
            return "선택된 파일 없음";
        }

        return mSelectedFileOrNull.Name + " · " + formatFileSize(mSelectedFileOrNull.Size);
    }
}
