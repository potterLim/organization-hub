using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    public async Task<ServiceResultOfT<GoogleDriveFolderNameSyncSummary>> ApplyStandardDriveFolderNamesAsync(CancellationToken cancellationToken)
    {
        GoogleDriveFolderNameSyncSummary summary = new GoogleDriveFolderNameSyncSummary();
        List<string> failureMessages = new List<string>();
        HashSet<string> appliedFolderIds = new HashSet<string>();
        DriveFolderNameSyncContext syncContext = new DriveFolderNameSyncContext(summary, failureMessages, appliedFolderIds);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.PublicFolderId, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, syncContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.CommonFolderId, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, syncContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.ResponsibleResourcesFolderId, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, syncContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.WorkResourcesFolderId, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, syncContext, cancellationToken);
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            await applyTeamFolderNamesAsync(setting, teams, syncContext, cancellationToken);
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

    private string createFolderNameFailureMessage(IReadOnlyList<string> failureMessages)
    {
        List<string> visibleMessages = failureMessages.Take(VISIBLE_FAILURE_MESSAGE_COUNT).ToList();
        string message = "일부 Drive 폴더명을 변경하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }
}
