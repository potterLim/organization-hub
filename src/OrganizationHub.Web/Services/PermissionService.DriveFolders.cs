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

public sealed partial class PermissionService
{
    private async Task<IReadOnlyList<DriveFolderReference>> getDriveFoldersAsync(UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, CancellationToken cancellationToken)
    {
        List<DriveFolderReference> folders = new List<DriveFolderReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalFoldersAsync(folders, settingOrNull, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    IReadOnlyList<DriveFolderReference> filteredTeamlessFolders = filterFolders(folders, accessMode);
                    IReadOnlyList<DriveFolderReference> allowedTeamlessFolders = await filterFoldersInsideOfficialRootAsync(filteredTeamlessFolders, cancellationToken);
                    return deduplicateFolderReferences(allowedTeamlessFolders);
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                await addTeamFoldersAsync(folders, team, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }
        }

        IReadOnlyList<DriveFolderReference> filteredFolders = filterFolders(folders, accessMode);
        IReadOnlyList<DriveFolderReference> allowedFolders = await filterFoldersInsideOfficialRootAsync(filteredFolders, cancellationToken);
        return deduplicateFolderReferences(allowedFolders);
    }
}
