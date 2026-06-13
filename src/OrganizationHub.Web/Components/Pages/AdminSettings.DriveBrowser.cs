using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private async Task openFolderAsync(DriveFileItem folder)
    {
        if (folder.IsFolder == false)
        {
            return;
        }

        GoogleDriveFolderPathItem pathItem = new GoogleDriveFolderPathItem();
        pathItem.ID = folder.ID;
        pathItem.Name = folder.Name;
        mFolderPath.Add(pathItem);
        await loadDriveItemsAsync();
    }

    private async Task openPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        await loadDriveItemsAsync();
    }

    private async Task goToDriveRootAsync()
    {
        resetDrivePathToRoot();
        await loadDriveItemsAsync();
    }

    private async Task goToOfficialRootAsync()
    {
        resetDrivePathToOfficialRoot();
        await loadDriveItemsAsync();
    }

    private void resetDrivePathToRoot()
    {
        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.ID = DRIVE_ROOT_ID;
        rootPathItem.Name = "내 Drive";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }

    private void resetDrivePathToOfficialRoot()
    {
        if (string.IsNullOrWhiteSpace(mFormModel.OfficialRootFolderID))
        {
            resetDrivePathToRoot();
            return;
        }

        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.ID = mFormModel.OfficialRootFolderID;
        rootPathItem.Name = "기본 자료";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }

    private List<DriveFileItem> getOrderedDriveItems()
    {
        return mDriveItems.OrderBy(item => item.IsFolder ? 0 : 1).ThenBy(item => item.IsFolder ? item.Name : string.Empty).ThenByDescending(item => item.ModifiedTime).ThenBy(item => item.Name).ToList();
    }

    private string getCurrentFolderID()
    {
        if (mFolderPath.Count == 0)
        {
            return DRIVE_ROOT_ID;
        }

        return mFolderPath[mFolderPath.Count - 1].ID;
    }

    private string getFileTypeDisplayName(DriveFileItem file)
    {
        if (string.IsNullOrWhiteSpace(file.MimeType))
        {
            return "파일";
        }

        if (file.MimeType == "application/pdf")
        {
            return "PDF";
        }

        if (file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "이미지";
        }

        return "파일";
    }

    private string formatDate(DateTime? dateOrNull)
    {
        if (dateOrNull.HasValue == false)
        {
            return "-";
        }

        return dateOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
    }
}
