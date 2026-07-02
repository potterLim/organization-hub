using System.Globalization;
using System.Linq;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    private string getSelectedRootDisplayName()
    {
        DriveFolderReference? rootFolderOrNull = mRootFolders.FirstOrDefault(folder => folder.FolderId.GetRequiredValue() == mSelectedRootFolderId);
        if (rootFolderOrNull == null)
        {
            return RootFolderLabel;
        }

        return rootFolderOrNull.DisplayName;
    }

    private string getBrowserTitle()
    {
        if (string.IsNullOrWhiteSpace(BrowserTitle) == false)
        {
            return BrowserTitle;
        }

        return getSelectedRootDisplayName();
    }

    private string getBrowserToolbarClass()
    {
        if (IsSearchEnabled)
        {
            return "hub-browser-toolbar";
        }

        return "hub-browser-toolbar hub-browser-toolbar-without-search";
    }

    private string getCountSummary()
    {
        int folderCount = mFiles.Count(file => file.IsFolder);
        int fileCount = mFiles.Count - folderCount;
        return "폴더 " + folderCount.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + fileCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private bool shouldShowFolderPath()
    {
        return mFolderPath.Count > 1;
    }

    private string getFileEmptyMessage()
    {
        if (string.IsNullOrWhiteSpace(mKeyword) == false)
        {
            return "검색 결과가 없습니다.";
        }

        if (mFolderPath.Count > 1)
        {
            return "이 폴더에는 아직 자료가 없습니다.";
        }

        return EmptyMessage;
    }

    private string getFileEmptyDescription()
    {
        if (string.IsNullOrWhiteSpace(mKeyword) == false)
        {
            return "검색어를 바꾸거나 현재 위치를 새로고침해 주세요.";
        }

        if (mFolderPath.Count > 1)
        {
            return "이 위치에 등록된 자료가 있으면 목록에 표시됩니다.";
        }

        return "자료가 등록되면 이 목록에 표시됩니다.";
    }
}
