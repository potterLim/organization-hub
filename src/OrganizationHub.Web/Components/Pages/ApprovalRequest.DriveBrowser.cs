using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private async Task handleSourceFolderChangedAsync(string folderID)
    {
        mFormModel.SourceFolderID = folderID;
        mFormModel.FileID = string.Empty;
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

    private async Task handleDestinationRootChangedAsync(string folderID)
    {
        mSelectedDestinationRootFolderID = folderID;
        DriveFolderReference? folderOrNull = getSelectedDestinationRootFolderOrNull();
        if (folderOrNull != null)
        {
            resetDestinationPath(folderOrNull);
        }

        await loadDestinationFoldersAsync();
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
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, sourceFolderOrNull.FolderKind, sourceFolderOrNull.FolderID, getCurrentSourceFolderID(), mKeyword, CancellationToken.None);
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

    private async Task loadDestinationFoldersAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? destinationFolderOrNull = getSelectedDestinationRootFolderOrNull();
        if (destinationFolderOrNull == null)
        {
            return;
        }

        mIsLoadingDestinationFolders = true;
        mDestinationFolderLoadErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, destinationFolderOrNull.FolderKind, destinationFolderOrNull.FolderID, getCurrentDestinationFolderID(), string.Empty, CancellationToken.None);
            mDestinationFoldersInCurrentFolder = files.Where(file => file.IsFolder).OrderBy(file => file.Name).ToList();
        }
        catch
        {
            mDestinationFoldersInCurrentFolder = new List<DriveFileItem>();
            mDestinationFolderLoadErrorMessage = "저장 위치를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingDestinationFolders = false;
        }
    }

    private async Task openSourceFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = file.ID;
        pathItem.Name = file.Name;
        mSourceFolderPath.Add(pathItem);
        mKeyword = string.Empty;
        await loadFilesAsync();
    }

    private async Task openDestinationFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = file.ID;
        pathItem.Name = file.Name;
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderID = file.ID;
        await loadDestinationFoldersAsync();
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

    private async Task navigateDestinationToPathAsync(int index)
    {
        if (index < 0 || index >= mDestinationFolderPath.Count)
        {
            return;
        }

        mDestinationFolderPath = mDestinationFolderPath.Take(index + 1).ToList();
        mFormModel.DestinationFolderID = getCurrentDestinationFolderID();
        await loadDestinationFoldersAsync();
    }

    private async Task createDestinationFolderAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        string folderName = getValidFolderNameOrEmpty(mNewDestinationFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            mMessage = "새 폴더 이름을 입력해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        PermissionDecision createDecision = await PermissionService.DecideStoreApprovedFileToFolderAsync(CurrentUserSessionOrNull, getCurrentDestinationFolderID(), CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            mMessage = createDecision.Reason;
            mMessageSeverity = Severity.Error;
            return;
        }

        mIsCreatingDestinationFolder = true;
        try
        {
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(getCurrentDestinationFolderID(), folderName, CancellationToken.None);
            mNewDestinationFolderName = string.Empty;
            mIsDestinationFolderCreateOpen = false;
            await loadDestinationFoldersAsync();
            await openDestinationFolderAsync(createdFolder);
            mMessage = "새 폴더를 만들었습니다.";
            mMessageSeverity = Severity.Success;
        }
        catch
        {
            mMessage = "폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.";
            mMessageSeverity = Severity.Error;
        }

        mIsCreatingDestinationFolder = false;
    }

    private void toggleDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = mIsDestinationFolderCreateOpen == false;
        if (mIsDestinationFolderCreateOpen == false)
        {
            mNewDestinationFolderName = string.Empty;
        }
    }

    private void closeDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = false;
        mNewDestinationFolderName = string.Empty;
    }

    private async Task handleDestinationFolderCreateKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter" && mIsCreatingDestinationFolder == false)
        {
            await createDestinationFolderAsync();
            return;
        }

        if (keyboardEventArgs.Key == "Escape")
        {
            closeDestinationFolderCreate();
        }
    }

    private void selectFile(DriveFileItem file)
    {
        mFormModel.FileID = file.ID;
        if (mHasCustomTitle == false || string.IsNullOrWhiteSpace(mFormModel.Title))
        {
            mFormModel.Title = createDefaultRequestTitle(file);
            mHasCustomTitle = false;
        }
    }

    private DriveFolderReference? getSelectedSourceFolderOrNull()
    {
        return mCreateContext.SourceFolders.FirstOrDefault(folder => folder.FolderID == mFormModel.SourceFolderID);
    }

    private DriveFolderReference? getSelectedDestinationRootFolderOrNull()
    {
        return mCreateContext.DestinationFolders.FirstOrDefault(folder => folder.FolderID == mSelectedDestinationRootFolderID);
    }

    private void resetSourcePath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = folder.FolderID;
        pathItem.Name = folder.DisplayName;
        mSourceFolderPath = new List<DriveFolderPathItem>();
        mSourceFolderPath.Add(pathItem);
    }

    private void resetDestinationPath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = folder.FolderID;
        pathItem.Name = folder.DisplayName;
        mDestinationFolderPath = new List<DriveFolderPathItem>();
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderID = folder.FolderID;
    }

    private string getCurrentSourceFolderID()
    {
        if (mSourceFolderPath.Count == 0)
        {
            return mFormModel.SourceFolderID;
        }

        return mSourceFolderPath[mSourceFolderPath.Count - 1].ID;
    }

    private string getCurrentDestinationFolderID()
    {
        if (mDestinationFolderPath.Count == 0)
        {
            return mSelectedDestinationRootFolderID;
        }

        return mDestinationFolderPath[mDestinationFolderPath.Count - 1].ID;
    }

    private string getValidFolderNameOrEmpty(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        string trimmedName = folderName.Trim();
        if (trimmedName.Length > 80)
        {
            return string.Empty;
        }

        if (trimmedName.Contains("/") || trimmedName.Contains("\\"))
        {
            return string.Empty;
        }

        return trimmedName;
    }

    private string getFileCountText()
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

        return "결재 요청할 파일이 없습니다.";
    }

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return dateTimeOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string getFileIcon(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return Icons.Material.Filled.Folder;
        }

        if (file.MimeType == "application/pdf")
        {
            return Icons.Material.Filled.PictureAsPdf;
        }

        return Icons.Material.Filled.InsertDriveFile;
    }
}
