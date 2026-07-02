using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IOrganizationPermissionService
{
    Task<bool> HasPermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken);

    Task<PermissionDecision> DecidePermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken);

    Task<PermissionDecision> DecidePermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        UserAccountId? ownerUserIdOrNull,
        CancellationToken cancellationToken);
}
