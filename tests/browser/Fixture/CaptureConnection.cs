using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.BrowserFixture;

public sealed class CaptureConnection : IGoogleConnectionService
{
    public Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(new GoogleConnectionStatus { HasOAuthConfiguration = CaptureState.Mode != "unconfigured", IsConnected = CaptureState.Mode != "disconnected" && CaptureState.Mode != "unconfigured", ConnectedEmail = "workspace@example.org", ConnectedAt = new DateTime(2026, 9, 1, 0, 0, 0), LastValidatedAt = DateTime.UtcNow, Message = "" });
    public string CreateAuthorizationUrl(GoogleOAuthRedirectUri uri) => "/admin/google?message=캡처용+연결+화면";
    public Task<ServiceResult> CompleteConnectionAsync(GoogleOAuthAuthorizationCode code, GoogleOAuthState state, GoogleOAuthRedirectUri uri, CancellationToken token) => Task.FromResult(ServiceResult.Succeed());
    public Task<ServiceResult> DisconnectAsync(CancellationToken token)
    {
        CaptureState.Mode = "disconnected";
        return Task.FromResult(ServiceResult.Succeed());
    }
}
