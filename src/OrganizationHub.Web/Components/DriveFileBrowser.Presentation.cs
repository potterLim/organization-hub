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

    private string getCountSummary()
    {
        int folderCount = mFiles.Count(file => file.IsFolder);
        int fileCount = mFiles.Count - folderCount;
        return "폴더 " + folderCount.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + fileCount.ToString(CultureInfo.InvariantCulture) + "개";
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
}
