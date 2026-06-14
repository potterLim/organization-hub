using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IPositionManagementService
{
    Task<IReadOnlyList<OrganizationPosition>> GetPositionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionDefinition>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken);
    Task<ServiceResult> CreatePositionAsync(PositionFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdatePositionAsync(PositionFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> ActivatePositionAsync(OrganizationPositionId positionId, CancellationToken cancellationToken);
    Task<ServiceResult> DeactivatePositionAsync(OrganizationPositionId positionId, CancellationToken cancellationToken);
}
