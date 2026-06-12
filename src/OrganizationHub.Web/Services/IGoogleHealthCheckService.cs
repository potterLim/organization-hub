using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleHealthCheckService
{
    Task<IReadOnlyList<GoogleHealthCheckItem>> GetHealthCheckItemsAsync(CancellationToken cancellationToken);
}
