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

    private GoogleFolderAssignmentOption createFolderAssignmentOption(string targetKey, string displayName, EGoogleFolderAssignmentKind folderAssignmentKind, int? teamID)
    {
        GoogleFolderAssignmentOption option = new GoogleFolderAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.FolderAssignmentKind = folderAssignmentKind;
        option.TeamID = teamID;
        return option;
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
