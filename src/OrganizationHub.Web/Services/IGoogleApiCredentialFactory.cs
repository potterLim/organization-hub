using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;

namespace OrganizationHub.Web.Services;

public interface IGoogleApiCredentialFactory
{
    Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken);
}
