using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private async Task loadCurrentFolderAsync()
    {
        try
        {
            GoogleDriveFolderPathItem currentFolder = mFolderPath[mFolderPath.Count - 1];
            IReadOnlyList<GoogleDriveFolderItem> folders = await GoogleWorkspaceBrowserService.ListDriveFoldersAsync(DriveFolderId.CreateOptional(currentFolder.Id), CancellationToken.None);
            mFolders = folders.ToList();
        }
        catch (Exception exception)
        {
            mMessage = createGoogleDriveErrorMessage(exception);
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task openFolderAsync(GoogleDriveFolderItem folder)
    {
        GoogleDriveFolderPathItem pathItem = new GoogleDriveFolderPathItem();
        pathItem.Id = folder.Id;
        pathItem.Name = folder.Name;
        mFolderPath.Add(pathItem);
        await loadCurrentFolderAsync();
    }

    private async Task openPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        await loadCurrentFolderAsync();
    }

    private async Task assignFolderAsync(GoogleDriveFolderItem folder)
    {
        ServiceResult folderAssignmentResult = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, DriveFolderId.CreateOptional(folder.Id), CancellationToken.None);
        showOperationResult(folderAssignmentResult, folderAssignmentResult.IsSuccess ? folder.Name + " 폴더를 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task ensureStandardDriveFoldersAsync()
    {
        mIsEnsuringStandardDriveFolders = true;
        try
        {
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> driveFolderSetupResult = await GoogleWorkspaceSetupService.EnsureStandardDriveFoldersAsync(CancellationToken.None);
            if (driveFolderSetupResult.IsSuccess && driveFolderSetupResult.ValueOrNull != null)
            {
                GoogleWorkspaceResourceSetupSummary summary = driveFolderSetupResult.ValueOrNull;
                mMessage = createSetupSummaryMessage("기본 Drive 리소스 준비 완료", summary);
                mMessageSeverity = Severity.Success;
                resetFolderPath();
                await loadAssignmentOptionsAsync();
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = driveFolderSetupResult.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardDriveFolders = false;
        }
    }

    private async Task applyStandardDriveFolderNamesAsync()
    {
        mIsApplyingStandardDriveFolderNames = true;
        try
        {
            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> folderNameSyncResult = await GoogleWorkspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);
            if (folderNameSyncResult.IsSuccess && folderNameSyncResult.ValueOrNull != null)
            {
                GoogleDriveFolderNameSyncSummary summary = folderNameSyncResult.ValueOrNull;
                mMessage = "Drive 폴더 정리 완료: 이름 변경 " + summary.RenamedCount.ToString(CultureInfo.InvariantCulture) + "개, 위치 정리 " + summary.MovedCount.ToString(CultureInfo.InvariantCulture) + "개, 폴더 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 이미 표준 " + summary.AlreadyStandardCount.ToString(CultureInfo.InvariantCulture) + "개";
                mMessageSeverity = Severity.Success;
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = folderNameSyncResult.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsApplyingStandardDriveFolderNames = false;
        }
    }

    private void resetFolderPath()
    {
        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.Id = "root";
        rootPathItem.Name = "내 Drive";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }

    private string createGoogleDriveErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleDriveMessage(exception, "Drive 폴더를 불러오지 못했습니다.");
    }
}
