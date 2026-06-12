using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ITeamManagementService
{
    Task<IReadOnlyList<Team>> GetTeamsAsync(CancellationToken cancellationToken);
    Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken);
    Task<ServiceResult> CreateTeamAsync(TeamFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateTeamAsync(TeamFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> ActivateTeamAsync(int teamID, CancellationToken cancellationToken);
    Task<ServiceResult> DeactivateTeamAsync(int teamID, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateGlobalSettingAsync(GlobalSettingFormModel formModel, CancellationToken cancellationToken);
}

