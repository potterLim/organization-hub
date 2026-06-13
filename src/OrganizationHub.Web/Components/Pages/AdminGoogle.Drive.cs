using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private async Task loadCurrentFolderAsync()
    {
        try
        {
            GoogleDriveFolderPathItem currentFolder = mFolderPath[mFolderPath.Count - 1];
            IReadOnlyList<GoogleDriveFolderItem> folders = await GoogleWorkspaceBrowserService.ListDriveFoldersAsync(currentFolder.ID, CancellationToken.None);
            mFolders = folders.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Drive 폴더를 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task openFolderAsync(GoogleDriveFolderItem folder)
    {
        GoogleDriveFolderPathItem pathItem = new GoogleDriveFolderPathItem();
        pathItem.ID = folder.ID;
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
        ServiceResult result = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, folder.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? folder.Name + " 폴더를 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task ensureStandardDriveFoldersAsync()
    {
        mIsEnsuringStandardDriveFolders = true;
        try
        {
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> result = await GoogleWorkspaceSetupService.EnsureStandardDriveFoldersAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleWorkspaceResourceSetupSummary summary = result.ValueOrNull;
                mMessage = createSetupSummaryMessage("기본 Drive 폴더 준비 완료", summary);
                mMessageSeverity = Severity.Success;
                resetFolderPath();
                await loadAssignmentOptionsAsync();
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
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
            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result = await GoogleWorkspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleDriveFolderNameSyncSummary summary = result.ValueOrNull;
                mMessage = "Drive 폴더 정리 완료: 이름 변경 " + summary.RenamedCount.ToString(CultureInfo.InvariantCulture) + "개, 위치 정리 " + summary.MovedCount.ToString(CultureInfo.InvariantCulture) + "개, 폴더 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 이미 표준 " + summary.AlreadyStandardCount.ToString(CultureInfo.InvariantCulture) + "개";
                mMessageSeverity = Severity.Success;
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
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
        rootPathItem.ID = "root";
        rootPathItem.Name = "내 Drive";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }
}
