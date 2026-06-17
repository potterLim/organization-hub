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
    public async Task<IReadOnlyList<GoogleFolderAssignmentOption>> GetFolderAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleFolderAssignmentOption> options = new List<GoogleFolderAssignmentOption>();
        options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.OfficialRootFolder, "기본 자료 위치", EGoogleFolderAssignmentKind.OfficialRoot, null));
        options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.PublicFolder, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, EGoogleFolderAssignmentKind.Public, null));
        options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.CommonFolder, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, EGoogleFolderAssignmentKind.Common, null));
        options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.ResponsibleResource, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, EGoogleFolderAssignmentKind.ResponsibleResources, null));
        options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.WorkResourcesFolder, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, EGoogleFolderAssignmentKind.WorkResources, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(team.Id);
                options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.CreateTeamMaterialsFolder(teamId), team.DisplayName + " 자료", EGoogleFolderAssignmentKind.TeamMaterials, teamId));
                options.Add(createFolderAssignmentOption(GoogleWorkspaceAssignmentTargetKey.CreateTeamApprovedFolder(teamId), team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, EGoogleFolderAssignmentKind.TeamApproved, teamId));
            }
        }

        return options;
    }

    public async Task<ServiceResult> AssignFolderAsync(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        if (targetKey.HasValue == false)
        {
            return ServiceResult.Fail("연결할 대상을 선택해 주세요.");
        }

        if (folderId.HasValue == false)
        {
            return ServiceResult.Fail("Drive 폴더를 선택해 주세요.");
        }

        IReadOnlyList<GoogleFolderAssignmentOption> options = await GetFolderAssignmentOptionsAsync(cancellationToken);
        GoogleFolderAssignmentOption? optionOrNull = options.FirstOrDefault(option => targetKey.Matches(option.TargetKey));
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("연결할 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            if (optionOrNull.FolderAssignmentKind != EGoogleFolderAssignmentKind.OfficialRoot)
            {
                ServiceResult rootValidationResult = await validateFolderInsideOfficialRootAsync(setting, folderId, cancellationToken);
                if (rootValidationResult.IsSuccess == false)
                {
                    return rootValidationResult;
                }
            }

            ServiceResult applyResult = await applyFolderAssignmentAsync(dbContext, setting, optionOrNull, folderId, cancellationToken);
            if (applyResult.IsSuccess == false)
            {
                return applyResult;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private GoogleFolderAssignmentOption createFolderAssignmentOption(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        string displayName,
        EGoogleFolderAssignmentKind folderAssignmentKind,
        OrganizationTeamId? teamIdOrNull
    )
    {
        GoogleFolderAssignmentOption option = new GoogleFolderAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.FolderAssignmentKind = folderAssignmentKind;
        option.TeamIdOrNull = teamIdOrNull;
        return option;
    }

    private async Task<ServiceResult> validateFolderInsideOfficialRootAsync(
        GlobalSetting setting,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(setting.OfficialRootFolderId))
        {
            return ServiceResult.Fail("먼저 기본 자료 위치를 지정해 주세요.");
        }

        DriveFolderId officialRootFolderId = DriveFolderId.CreateOptional(setting.OfficialRootFolderId);
        if (folderId == officialRootFolderId)
        {
            return ServiceResult.Succeed();
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        if (ancestorFolderIds.Contains(officialRootFolderId))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("기본 자료 위치 안의 폴더만 연결할 수 있습니다.");
    }

    private async Task<ServiceResult> applyFolderAssignmentAsync(
        OrganizationHubDbContext dbContext,
        GlobalSetting setting,
        GoogleFolderAssignmentOption option,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        switch (option.FolderAssignmentKind)
        {
            case EGoogleFolderAssignmentKind.OfficialRoot:
                setting.OfficialRootFolderId = folderId.GetRequiredValue();
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.Public:
                setting.PublicFolderId = folderId.GetRequiredValue();
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.Common:
                setting.CommonFolderId = folderId.GetRequiredValue();
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.ResponsibleResources:
                setting.ResponsibleResourcesFolderId = folderId.GetRequiredValue();
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.WorkResources:
                setting.WorkResourcesFolderId = folderId.GetRequiredValue();
                return ServiceResult.Succeed();
            case EGoogleFolderAssignmentKind.TeamMaterials:
                return await assignTeamFolderAsync(dbContext, option, folderId, EGoogleFolderAssignmentKind.TeamMaterials, cancellationToken);
            case EGoogleFolderAssignmentKind.TeamApproved:
                return await assignTeamFolderAsync(dbContext, option, folderId, EGoogleFolderAssignmentKind.TeamApproved, cancellationToken);
            default:
                return ServiceResult.Fail("지원하지 않는 폴더 연결 대상입니다.");
        }
    }

    private async Task<ServiceResult> assignTeamFolderAsync(
        OrganizationHubDbContext dbContext,
        GoogleFolderAssignmentOption option,
        DriveFolderId folderId,
        EGoogleFolderAssignmentKind folderAssignmentKind,
        CancellationToken cancellationToken
    )
    {
        int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(option.TeamIdOrNull);
        if (teamIdValueOrNull.HasValue == false)
        {
            return ServiceResult.Fail("팀 폴더 대상을 찾을 수 없습니다.");
        }

        int teamId = teamIdValueOrNull.GetValueOrDefault();
        Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);
        if (teamOrNull == null)
        {
            return ServiceResult.Fail("팀을 찾을 수 없습니다.");
        }

        if (folderAssignmentKind == EGoogleFolderAssignmentKind.TeamMaterials)
        {
            teamOrNull.MaterialsFolderId = folderId.GetRequiredValue();
            return ServiceResult.Succeed();
        }

        teamOrNull.ApprovedFolderId = folderId.GetRequiredValue();
        return ServiceResult.Succeed();
    }
}
