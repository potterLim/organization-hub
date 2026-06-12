using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> CreateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> ActivateUserAsync(UserSession userSession, int userID, CancellationToken cancellationToken);
    Task<ServiceResult> DeactivateUserAsync(UserSession userSession, int userID, CancellationToken cancellationToken);
    Task<ServiceResult> ResetPasswordAsync(UserSession userSession, int userID, string newPassword, CancellationToken cancellationToken);
}

