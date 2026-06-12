using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleConnectionService
{
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken);
    string CreateAuthorizationUrl(string redirectUri);
    Task<ServiceResult> CompleteConnectionAsync(string code, string state, string redirectUri, CancellationToken cancellationToken);
    Task<ServiceResult> DisconnectAsync(CancellationToken cancellationToken);
}
