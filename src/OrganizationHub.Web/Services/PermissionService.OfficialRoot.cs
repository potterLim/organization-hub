using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<bool> canUseFolderInsideOfficialRootAsync(string folderID, CancellationToken cancellationToken)
    {
        string officialRootFolderID = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderID = getNullAsEmpty(settingOrNull.OfficialRootFolderID);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return false;
        }

        if (folderID == officialRootFolderID)
        {
            return true;
        }

        return await isFolderInsideOfficialRootAsync(folderID, officialRootFolderID, cancellationToken);
    }

    private async Task<IReadOnlyList<DriveFolderReference>> filterFoldersInsideOfficialRootAsync(IReadOnlyList<DriveFolderReference> folders, CancellationToken cancellationToken)
    {
        string officialRootFolderID = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderID = getNullAsEmpty(settingOrNull.OfficialRootFolderID);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return new List<DriveFolderReference>();
        }

        List<Task<DriveFolderFilterResult>> filterTasks = new List<Task<DriveFolderFilterResult>>();
        foreach (DriveFolderReference folder in folders)
        {
            filterTasks.Add(createDriveFolderFilterResultAsync(folder, officialRootFolderID, cancellationToken));
        }

        DriveFolderFilterResult[] filterResults = await Task.WhenAll(filterTasks);
        List<DriveFolderReference> allowedFolders = new List<DriveFolderReference>();
        foreach (DriveFolderFilterResult filterResult in filterResults)
        {
            if (filterResult.IsAllowed)
            {
                allowedFolders.Add(filterResult.Folder);
            }
        }

        return allowedFolders;
    }

    private async Task<DriveFolderFilterResult> createDriveFolderFilterResultAsync(DriveFolderReference folder, string officialRootFolderID, CancellationToken cancellationToken)
    {
        if (folder.FolderID == officialRootFolderID)
        {
            return new DriveFolderFilterResult(folder, EPermissionDecisionState.Allowed);
        }

        bool isAllowed = await isFolderInsideOfficialRootAsync(folder.FolderID, officialRootFolderID, cancellationToken);
        EPermissionDecisionState decisionState = isAllowed ? EPermissionDecisionState.Allowed : EPermissionDecisionState.Denied;
        return new DriveFolderFilterResult(folder, decisionState);
    }

    private async Task<bool> isFolderInsideOfficialRootAsync(string folderID, string officialRootFolderID, CancellationToken cancellationToken)
    {
        string cacheKey = "OrganizationHub:FolderInsideOfficialRoot:" + officialRootFolderID + ":" + folderID;
        object? cachedValueOrNull;
        if (mMemoryCache.TryGetValue(cacheKey, out cachedValueOrNull) && cachedValueOrNull is bool cachedResult)
        {
            return cachedResult;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        bool isInsideRoot = ancestorFolderIDs.Contains(officialRootFolderID);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
        mMemoryCache.Set(cacheKey, isInsideRoot, cacheEntryOptions);
        return isInsideRoot;
    }

    private bool isInsideOfficialRoot(IReadOnlySet<string> ancestorFolderIDs, string officialRootFolderID)
    {
        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return false;
        }

        return ancestorFolderIDs.Contains(officialRootFolderID);
    }
}
