using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private async Task handleSourceFolderChangedAsync(string folderId)
    {
        mFormModel.SourceFolderId = folderId;
        mFormModel.FileId = string.Empty;
        mSuggestedRequestTitle = string.Empty;
        if (mHasCustomTitle == false)
        {
            mFormModel.Title = string.Empty;
        }

        mKeyword = string.Empty;
        DriveFolderReference? folderOrNull = getSelectedSourceFolderOrNull();
        if (folderOrNull != null)
        {
            resetSourcePath(folderOrNull);
        }

        await loadFilesAsync();
    }

    private async Task loadFilesAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? sourceFolderOrNull = getSelectedSourceFolderOrNull();
        if (sourceFolderOrNull == null)
        {
            return;
        }

        mIsLoadingFiles = true;
        mFileLoadErrorMessage = string.Empty;
        try
        {
            DriveFolderBrowseRequest request = new DriveFolderBrowseRequest(CurrentUserSessionOrNull, sourceFolderOrNull.FolderKind, sourceFolderOrNull.FolderId, DriveFolderId.CreateOptional(getCurrentSourceFolderId()));
            request.Keyword = SearchKeyword.CreateOptional(mKeyword);
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(request, CancellationToken.None);
            mFiles = files.ToList();
        }
        catch
        {
            mFiles = new List<DriveFileItem>();
            mFileLoadErrorMessage = "결재할 파일을 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingFiles = false;
        }
    }

    private async Task openSourceFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.Id = file.Id;
        pathItem.Name = file.Name;
        mSourceFolderPath.Add(pathItem);
        mKeyword = string.Empty;
        await loadFilesAsync();
    }

    private async Task handleCompactFileClickAsync(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            await openSourceFolderAsync(file);
            return;
        }

        selectFile(file);
    }

    private async Task navigateSourceToPathAsync(int index)
    {
        if (index < 0 || index >= mSourceFolderPath.Count)
        {
            return;
        }

        mSourceFolderPath = mSourceFolderPath.Take(index + 1).ToList();
        mKeyword = string.Empty;
        await loadFilesAsync();
    }

    private void selectFile(DriveFileItem file)
    {
        mFormModel.FileId = file.Id;
        mSuggestedRequestTitle = createDefaultRequestTitle(file);
    }

    private DriveFolderReference? getSelectedSourceFolderOrNull()
    {
        return mCreateContext.SourceFolders.FirstOrDefault(folder => folder.FolderId.GetRequiredValue() == mFormModel.SourceFolderId);
    }

    private void resetSourcePath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.Id = folder.FolderId.GetRequiredValue();
        pathItem.Name = folder.DisplayName;
        mSourceFolderPath = new List<DriveFolderPathItem>();
        mSourceFolderPath.Add(pathItem);
    }

    private string getCurrentSourceFolderId()
    {
        if (mSourceFolderPath.Count == 0)
        {
            return mFormModel.SourceFolderId;
        }

        return mSourceFolderPath[mSourceFolderPath.Count - 1].Id;
    }
}
