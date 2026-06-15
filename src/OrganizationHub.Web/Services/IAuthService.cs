using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IAuthService
{
    Task<LoginResult> ValidateLoginAsync(Username username, PlainPassword password, CancellationToken cancellationToken);
    ClaimsPrincipal CreateClaimsPrincipal(UserAccount userAccount);
}
