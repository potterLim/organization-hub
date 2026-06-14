using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleConnectionService
{
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken);

    string CreateAuthorizationUrl(GoogleOAuthRedirectUri redirectUri);

    Task<ServiceResult> CompleteConnectionAsync(
        GoogleOAuthAuthorizationCode authorizationCode,
        GoogleOAuthState state,
        GoogleOAuthRedirectUri redirectUri,
        CancellationToken cancellationToken);

    Task<ServiceResult> DisconnectAsync(CancellationToken cancellationToken);
}
