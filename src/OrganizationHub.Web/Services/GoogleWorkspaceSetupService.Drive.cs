using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
    private const string DRIVE_FOLDER_CACHE_PREFIX = "OrganizationHub:GoogleWorkspace:DriveFolders:";

    public async Task<IReadOnlyList<GoogleFolderAssignmentOption>> GetFolderAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleFolderAssignmentOption> options = new List<GoogleFolderAssignmentOption>();
        options.Add(createFolderAssignmentOption("OfficialRoot", "기본 자료 위치", EGoogleFolderAssignmentKind.OfficialRoot, null));
        options.Add(createFolderAssignmentOption("Public", OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, EGoogleFolderAssignmentKind.Public, null));
        options.Add(createFolderAssignmentOption("Common", OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, EGoogleFolderAssignmentKind.Common, null));
        options.Add(createFolderAssignmentOption("Leader", OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, EGoogleFolderAssignmentKind.ResponsibleResources, null));
        options.Add(createFolderAssignmentOption("WorkResources", OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, EGoogleFolderAssignmentKind.WorkResources, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                options.Add(createFolderAssignmentOption("TeamMaterials:" + team.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), team.DisplayName + " 자료", EGoogleFolderAssignmentKind.TeamMaterials, team.ID));
                options.Add(createFolderAssignmentOption("TeamApproved:" + team.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, EGoogleFolderAssignmentKind.TeamApproved, team.ID));
            }
        }

        return options;
    }

    public async Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardDriveFoldersAsync(CancellationToken cancellationToken)
    {
        GoogleWorkspaceResourceSetupSummary summary = new GoogleWorkspaceResourceSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                DriveFileItem officialRoot = await getOrCreateDriveFolderAsync("root", OrganizationHubProduct.NAME, summary, cancellationToken);
                assignIfChanged(setting.OfficialRootFolderID, officialRoot.ID, value => setting.OfficialRootFolderID = value, summary);
                DriveFileItem publicFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.PublicFolderID, publicFolder.ID, value => setting.PublicFolderID = value, summary);
                DriveFileItem commonFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.CommonFolderID, commonFolder.ID, value => setting.CommonFolderID = value, summary);
                DriveFileItem responsibleResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.ResponsibleResourcesFolderID, responsibleResourcesFolder.ID, value => setting.ResponsibleResourcesFolderID = value, summary);
                DriveFileItem workResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.WorkResourcesFolderID, workResourcesFolder.ID, value => setting.WorkResourcesFolderID = value, summary);
                DriveFileItem teamRootFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME, summary, cancellationToken);
                List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
                foreach (Team team in teams)
                {
                    await ensureTeamDriveFoldersAsync(team, officialRoot.ID, teamRootFolder.ID, summary, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                clearDriveFolderCaches(officialRoot.ID, teamRootFolder.ID);
            }
        }
        catch (Exception exception)
        {
            failureMessages.Add("기본 Drive 폴더를 준비하지 못했습니다. " + exception.Message);
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail(createResourceSetupFailureMessage("Drive 폴더", failureMessages));
        }

        return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Succeed(summary);
    }

    public async Task<ServiceResultOfT<GoogleDriveFolderNameSyncSummary>> ApplyStandardDriveFolderNamesAsync(CancellationToken cancellationToken)
    {
        GoogleDriveFolderNameSyncSummary summary = new GoogleDriveFolderNameSyncSummary();
        List<string> failureMessages = new List<string>();
        HashSet<string> appliedFolderIDs = new HashSet<string>();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.PublicFolderID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.CommonFolderID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.ResponsibleResourcesFolderID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.WorkResourcesFolderID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            await applyTeamFolderNamesAsync(setting, teams, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Fail(createFolderNameFailureMessage(failureMessages));
        }

        if (summary.TargetCount == 0)
        {
            return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Fail("표준 이름을 적용할 Drive 폴더가 아직 설정되지 않았습니다.");
        }

        return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Succeed(summary);
    }

    public async Task<ServiceResult> AssignFolderAsync(string targetKey, string folderID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return ServiceResult.Fail("저장 대상을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(folderID))
        {
            return ServiceResult.Fail("Drive 폴더를 선택해 주세요.");
        }

        IReadOnlyList<GoogleFolderAssignmentOption> options = await GetFolderAssignmentOptionsAsync(cancellationToken);
        GoogleFolderAssignmentOption? optionOrNull = options.FirstOrDefault(option => option.TargetKey == targetKey);
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("저장 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            if (optionOrNull.FolderAssignmentKind != EGoogleFolderAssignmentKind.OfficialRoot)
            {
                ServiceResult rootValidationResult = await validateFolderInsideOfficialRootAsync(setting, folderID, cancellationToken);
                if (rootValidationResult.IsSuccess == false)
                {
                    return rootValidationResult;
                }
            }

            ServiceResult applyResult = await applyFolderAssignmentAsync(dbContext, setting, optionOrNull, folderID, cancellationToken);
            if (applyResult.IsSuccess == false)
            {
                return applyResult;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private async Task applyTeamFolderNamesAsync(GlobalSetting setting, IReadOnlyList<Team> teams, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        List<GoogleWorkspaceTeamFolderNameContext> teamFolderNameContexts = new List<GoogleWorkspaceTeamFolderNameContext>();
        Dictionary<string, int> parentFolderUsageCounts = new Dictionary<string, int>();
        HashSet<string> teamRootCandidateFolderIDs = new HashSet<string>();
        foreach (Team team in teams)
        {
            DriveFileMetadata? materialsMetadataOrNull = await applyFolderNameIfConfiguredAsync(team.MaterialsFolderID, OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            DriveFileMetadata? approvedMetadataOrNull = await applyFolderNameIfConfiguredAsync(team.ApprovedFolderID, OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            IReadOnlySet<string> teamParentFolderIDs = getSharedParentFolderIDs(materialsMetadataOrNull, approvedMetadataOrNull, setting.OfficialRootFolderID);
            if (teamParentFolderIDs.Count != 1)
            {
                continue;
            }

            string teamParentFolderID = teamParentFolderIDs.First();
            teamFolderNameContexts.Add(new GoogleWorkspaceTeamFolderNameContext(team, teamParentFolderID, materialsMetadataOrNull, approvedMetadataOrNull));
            if (parentFolderUsageCounts.ContainsKey(teamParentFolderID))
            {
                parentFolderUsageCounts[teamParentFolderID] = parentFolderUsageCounts[teamParentFolderID] + 1;
            }
            else
            {
                parentFolderUsageCounts[teamParentFolderID] = 1;
            }
        }

        foreach (GoogleWorkspaceTeamFolderNameContext teamFolderNameContext in teamFolderNameContexts)
        {
            if (parentFolderUsageCounts[teamFolderNameContext.TeamParentFolderID] > 1)
            {
                teamRootCandidateFolderIDs.Add(teamFolderNameContext.TeamParentFolderID);
                DriveFileMetadata? nestedTeamParentMetadataOrNull = await getOrCreateTeamParentFolderAsync(teamFolderNameContext.TeamParentFolderID, teamFolderNameContext.Team.DisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
                if (nestedTeamParentMetadataOrNull != null)
                {
                    await moveFolderIfNeededAsync(teamFolderNameContext.MaterialsMetadataOrNull, nestedTeamParentMetadataOrNull.ID, teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, failureMessages, cancellationToken);
                    await moveFolderIfNeededAsync(teamFolderNameContext.ApprovedMetadataOrNull, nestedTeamParentMetadataOrNull.ID, teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, failureMessages, cancellationToken);
                }

                continue;
            }

            DriveFileMetadata? teamParentMetadataOrNull = await applyFolderNameByIDAsync(teamFolderNameContext.TeamParentFolderID, teamFolderNameContext.Team.DisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
            if (teamParentMetadataOrNull == null)
            {
                continue;
            }

            foreach (string parentFolderID in teamParentMetadataOrNull.ParentFolderIDs)
            {
                if (isOfficialRootFolderID(parentFolderID, setting.OfficialRootFolderID) == false)
                {
                    teamRootCandidateFolderIDs.Add(parentFolderID);
                }
            }
        }

        if (teamRootCandidateFolderIDs.Count == 1)
        {
            string teamRootFolderID = teamRootCandidateFolderIDs.First();
            await applyFolderNameByIDAsync(teamRootFolderID, OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }
    }

    private async Task ensureTeamDriveFoldersAsync(Team team, string officialRootFolderID, string teamRootFolderID, GoogleWorkspaceResourceSetupSummary summary, CancellationToken cancellationToken)
    {
        if (team.TeamKey == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY)
        {
            DriveFileItem managementFolder = await getOrCreateDriveFolderAsync(officialRootFolderID, OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.MaterialsFolderID, managementFolder.ID, value => team.MaterialsFolderID = value, summary);
            DriveFileItem managementApprovedFolder = await getOrCreateDriveFolderAsync(officialRootFolderID, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.ApprovedFolderID, managementApprovedFolder.ID, value => team.ApprovedFolderID = value, summary);
            return;
        }

        DriveFileItem teamFolder = await getOrCreateDriveFolderAsync(teamRootFolderID, team.DisplayName, summary, cancellationToken);
        DriveFileItem materialsFolder = await getOrCreateDriveFolderAsync(teamFolder.ID, OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.MaterialsFolderID, materialsFolder.ID, value => team.MaterialsFolderID = value, summary);
        DriveFileItem approvedFolder = await getOrCreateDriveFolderAsync(teamFolder.ID, OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.ApprovedFolderID, approvedFolder.ID, value => team.ApprovedFolderID = value, summary);
    }

    private async Task<DriveFileItem> getOrCreateDriveFolderAsync(string parentFolderID, string folderName, GoogleWorkspaceResourceSetupSummary summary, CancellationToken cancellationToken)
    {
        summary.TargetCount = summary.TargetCount + 1;
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolderID, cancellationToken);
        DriveFileItem? existingFolderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (existingFolderOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingFolderOrNull;
        }

        DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(parentFolderID, folderName, cancellationToken);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdFolder;
    }

    private void assignIfChanged(string? currentValueOrNull, string nextValue, Action<string> assignAction, GoogleWorkspaceResourceSetupSummary summary)
    {
        if (string.Equals(getNullAsEmpty(currentValueOrNull), nextValue, StringComparison.Ordinal))
        {
            return;
        }

        assignAction(nextValue);
        summary.AssignedCount = summary.AssignedCount + 1;
    }

    private async Task<DriveFileMetadata?> getOrCreateTeamParentFolderAsync(string teamRootFolderID, string teamDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        DriveFileMetadata? childFolderOrNull = await getChildFolderByNameOrNullAsync(teamRootFolderID, teamDisplayName, cancellationToken);
        if (childFolderOrNull != null)
        {
            return await applyFolderNameByIDAsync(childFolderOrNull.ID, teamDisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }

        return await createTeamParentFolderAsync(teamRootFolderID, teamDisplayName, summary, failureMessages, cancellationToken);
    }

    private async Task<DriveFileMetadata?> createTeamParentFolderAsync(string teamRootFolderID, string teamDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, CancellationToken cancellationToken)
    {
        try
        {
            DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(teamRootFolderID, teamDisplayName, cancellationToken);
            summary.CreatedCount = summary.CreatedCount + 1;
            DriveFileMetadata? createdMetadataOrNull = await mDriveService.GetFileOrNullAsync(createdFolder.ID, cancellationToken);
            if (createdMetadataOrNull != null)
            {
                return createdMetadataOrNull;
            }

            DriveFileMetadata createdMetadata = new DriveFileMetadata();
            createdMetadata.ID = createdFolder.ID;
            createdMetadata.Name = createdFolder.Name;
            createdMetadata.MimeType = GOOGLE_DRIVE_FOLDER_MIME_TYPE;
            createdMetadata.ParentFolderIDs = new List<string> { teamRootFolderID };
            return createdMetadata;
        }
        catch (Exception exception)
        {
            failureMessages.Add(teamDisplayName + " 폴더를 만들지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task<DriveFileMetadata?> getChildFolderByNameOrNullAsync(string parentFolderID, string folderName, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolderID, cancellationToken);
        DriveFileItem? folderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (folderOrNull == null)
        {
            return null;
        }

        return await mDriveService.GetFileOrNullAsync(folderOrNull.ID, cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameIfConfiguredAsync(string? folderIDOrNull, string standardName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderIDOrNull))
        {
            summary.SkippedCount = summary.SkippedCount + 1;
            return null;
        }

        string folderID = folderIDOrNull.Trim();
        return await applyFolderNameByIDAsync(folderID, standardName, summary, failureMessages, appliedFolderIDs, cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameByIDAsync(string folderID, string standardName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        if (appliedFolderIDs.Contains(folderID))
        {
            return await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        }

        appliedFolderIDs.Add(folderID);
        summary.TargetCount = summary.TargetCount + 1;
        try
        {
            DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
            if (metadataOrNull == null)
            {
                failureMessages.Add(standardName + " 폴더를 찾지 못했습니다.");
                return null;
            }

            if (metadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
            {
                failureMessages.Add(standardName + " 저장 대상이 폴더가 아닙니다.");
                return null;
            }

            if (metadataOrNull.Name == standardName)
            {
                summary.AlreadyStandardCount = summary.AlreadyStandardCount + 1;
                return metadataOrNull;
            }

            DriveFileMetadata renamedMetadata = await mDriveService.RenameDriveItemAsync(folderID, standardName, cancellationToken);
            summary.RenamedCount = summary.RenamedCount + 1;
            return renamedMetadata;
        }
        catch (Exception exception)
        {
            failureMessages.Add(standardName + " 폴더명을 변경하지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task moveFolderIfNeededAsync(DriveFileMetadata? folderMetadataOrNull, string? targetParentFolderIDOrNull, string folderDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, CancellationToken cancellationToken)
    {
        if (folderMetadataOrNull == null || string.IsNullOrWhiteSpace(targetParentFolderIDOrNull))
        {
            return;
        }

        string targetParentFolderID = targetParentFolderIDOrNull.Trim();
        if (folderMetadataOrNull.ParentFolderIDs.Contains(targetParentFolderID))
        {
            return;
        }

        try
        {
            await mDriveService.MoveDriveItemToFolderAsync(folderMetadataOrNull.ID, targetParentFolderID, cancellationToken);
            summary.MovedCount = summary.MovedCount + 1;
        }
        catch (Exception exception)
        {
            failureMessages.Add(folderDisplayName + " 폴더 위치를 정리하지 못했습니다. " + exception.Message);
        }
    }

    private IReadOnlySet<string> getSharedParentFolderIDs(DriveFileMetadata? firstMetadataOrNull, DriveFileMetadata? secondMetadataOrNull, string? officialRootFolderIDOrNull)
    {
        HashSet<string> sharedParentFolderIDs = new HashSet<string>();
        if (firstMetadataOrNull == null || secondMetadataOrNull == null)
        {
            return sharedParentFolderIDs;
        }

        HashSet<string> secondParentFolderIDs = new HashSet<string>(secondMetadataOrNull.ParentFolderIDs);
        foreach (string parentFolderID in firstMetadataOrNull.ParentFolderIDs)
        {
            if (secondParentFolderIDs.Contains(parentFolderID) && isOfficialRootFolderID(parentFolderID, officialRootFolderIDOrNull) == false)
            {
                sharedParentFolderIDs.Add(parentFolderID);
            }
        }

        return sharedParentFolderIDs;
    }

    private bool isOfficialRootFolderID(string folderID, string? officialRootFolderIDOrNull)
    {
        return string.IsNullOrWhiteSpace(officialRootFolderIDOrNull) == false && folderID == officialRootFolderIDOrNull.Trim();
    }

    private string createFolderNameFailureMessage(IReadOnlyList<string> failureMessages)
    {
        List<string> visibleMessages = failureMessages.Take(3).ToList();
        string message = "일부 Drive 폴더명을 변경하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }

    private GoogleFolderAssignmentOption createFolderAssignmentOption(string targetKey, string displayName, EGoogleFolderAssignmentKind folderAssignmentKind, int? teamID)
    {
        GoogleFolderAssignmentOption option = new GoogleFolderAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.FolderAssignmentKind = folderAssignmentKind;
        option.TeamID = teamID;
        return option;
    }

    private void clearDriveFolderCaches(params string[] folderIDs)
    {
        if (mMemoryCacheOrNull == null)
        {
            return;
        }

        mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + "root");
        foreach (string folderID in folderIDs)
        {
            if (string.IsNullOrWhiteSpace(folderID))
            {
                continue;
            }

            mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + folderID);
        }
    }

    private async Task<ServiceResult> validateFolderInsideOfficialRootAsync(GlobalSetting setting, string folderID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(setting.OfficialRootFolderID))
        {
            return ServiceResult.Fail("먼저 기본 자료 위치를 지정해 주세요.");
        }

        string officialRootFolderID = setting.OfficialRootFolderID.Trim();
        if (folderID == officialRootFolderID)
        {
            return ServiceResult.Succeed();
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        if (ancestorFolderIDs.Contains(officialRootFolderID))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("기본 자료 위치 안의 폴더만 저장 대상으로 지정할 수 있습니다.");
    }

    private async Task<ServiceResult> applyFolderAssignmentAsync(OrganizationHubDbContext dbContext, GlobalSetting setting, GoogleFolderAssignmentOption option, string folderID, CancellationToken cancellationToken)
    {
        string trimmedFolderID = folderID.Trim();
        switch (option.FolderAssignmentKind)
        {
            case EGoogleFolderAssignmentKind.OfficialRoot:
                setting.OfficialRootFolderID = trimmedFolderID;
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.Public:
                setting.PublicFolderID = trimmedFolderID;
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.Common:
                setting.CommonFolderID = trimmedFolderID;
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.ResponsibleResources:
                setting.ResponsibleResourcesFolderID = trimmedFolderID;
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.WorkResources:
                setting.WorkResourcesFolderID = trimmedFolderID;
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.TeamMaterials:
                return await assignTeamFolderAsync(dbContext, option, trimmedFolderID, EGoogleFolderAssignmentKind.TeamMaterials, cancellationToken);
            case EGoogleFolderAssignmentKind.TeamApproved:
                return await assignTeamFolderAsync(dbContext, option, trimmedFolderID, EGoogleFolderAssignmentKind.TeamApproved, cancellationToken);
            default:
                return ServiceResult.Fail("지원하지 않는 폴더 저장 대상입니다.");
        }
    }

    private async Task<ServiceResult> assignTeamFolderAsync(OrganizationHubDbContext dbContext, GoogleFolderAssignmentOption option, string folderID, EGoogleFolderAssignmentKind folderAssignmentKind, CancellationToken cancellationToken)
    {
        if (option.TeamID.HasValue == false)
        {
            return ServiceResult.Fail("팀 폴더 대상을 찾을 수 없습니다.");
        }

        int teamID = option.TeamID.Value;
        Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == teamID, cancellationToken);
        if (teamOrNull == null)
        {
            return ServiceResult.Fail("팀을 찾을 수 없습니다.");
        }

        if (folderAssignmentKind == EGoogleFolderAssignmentKind.TeamMaterials)
        {
            teamOrNull.MaterialsFolderID = folderID;
            return ServiceResult.Succeed();
        }

        teamOrNull.ApprovedFolderID = folderID;
        return ServiceResult.Succeed();
    }
}
