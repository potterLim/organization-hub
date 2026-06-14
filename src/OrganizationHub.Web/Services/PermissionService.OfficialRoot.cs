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
    private async Task<bool> canUseFolderInsideOfficialRootAsync(DriveFolderId folderId, CancellationToken cancellationToken)
    {
        string officialRootFolderId = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderId = getTextOrEmpty(settingOrNull.OfficialRootFolderId);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderId))
        {
            return false;
        }

        DriveFolderId officialRootFolder = DriveFolderId.CreateOptional(officialRootFolderId);
        if (folderId == officialRootFolder)
        {
            return true;
        }

        return await isFolderInsideOfficialRootAsync(folderId, officialRootFolder, cancellationToken);
    }

    private async Task<IReadOnlyList<DriveFolderReference>> filterFoldersInsideOfficialRootAsync(
        IReadOnlyList<DriveFolderReference> folders,
        CancellationToken cancellationToken
    )
    {
        string officialRootFolderId = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderId = getTextOrEmpty(settingOrNull.OfficialRootFolderId);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderId))
        {
            return new List<DriveFolderReference>();
        }

        List<Task<DriveFolderFilterResult>> filterTasks = new List<Task<DriveFolderFilterResult>>();
        foreach (DriveFolderReference folder in folders)
        {
            filterTasks.Add(createDriveFolderFilterResultAsync(folder, DriveFolderId.CreateOptional(officialRootFolderId), cancellationToken));
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

    private async Task<DriveFolderFilterResult> createDriveFolderFilterResultAsync(
        DriveFolderReference folder,
        DriveFolderId officialRootFolderId,
        CancellationToken cancellationToken
    )
    {
        if (folder.FolderId == officialRootFolderId)
        {
            return new DriveFolderFilterResult(folder, EPermissionDecisionState.Allowed);
        }

        bool isAllowed = await isFolderInsideOfficialRootAsync(folder.FolderId, officialRootFolderId, cancellationToken);
        EPermissionDecisionState decisionState = isAllowed ? EPermissionDecisionState.Allowed : EPermissionDecisionState.Denied;
        return new DriveFolderFilterResult(folder, decisionState);
    }

    private async Task<bool> isFolderInsideOfficialRootAsync(
        DriveFolderId folderId,
        DriveFolderId officialRootFolderId,
        CancellationToken cancellationToken
    )
    {
        string cacheKey = "OrganizationHub:FolderInsideOfficialRoot:" + officialRootFolderId.GetRequiredValue() + ":" + folderId.GetRequiredValue();
        object? cachedValueOrNull;
        if (mMemoryCache.TryGetValue(cacheKey, out cachedValueOrNull) && cachedValueOrNull is bool cachedResult)
        {
            return cachedResult;
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        bool isInsideRoot = ancestorFolderIds.Contains(officialRootFolderId);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
        mMemoryCache.Set(cacheKey, isInsideRoot, cacheEntryOptions);
        return isInsideRoot;
    }

    private bool isInsideOfficialRoot(IReadOnlySet<DriveFolderId> ancestorFolderIds, DriveFolderId officialRootFolderId)
    {
        if (officialRootFolderId.HasValue == false)
        {
            return false;
        }

        return ancestorFolderIds.Contains(officialRootFolderId);
    }
}
