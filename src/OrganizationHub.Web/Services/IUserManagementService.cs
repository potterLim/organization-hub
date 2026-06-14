using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> CreateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> ActivateUserAsync(UserSession userSession, UserAccountId userId, CancellationToken cancellationToken);
    Task<ServiceResult> DeactivateUserAsync(UserSession userSession, UserAccountId userId, CancellationToken cancellationToken);
    Task<ServiceResult> ResetPasswordAsync(UserSession userSession, UserAccountId userId, PlainPassword newPassword, CancellationToken cancellationToken);
}
