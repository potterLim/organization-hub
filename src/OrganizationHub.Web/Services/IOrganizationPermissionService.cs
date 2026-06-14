using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IOrganizationPermissionService
{
    Task<bool> UsesConfiguredPermissionsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, CancellationToken cancellationToken);
    Task<PermissionDecision> DecidePermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, CancellationToken cancellationToken);
    Task<PermissionDecision> DecidePermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, int? ownerUserIDOrNull, CancellationToken cancellationToken);
}
