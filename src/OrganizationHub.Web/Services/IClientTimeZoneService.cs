using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Services;

public interface IClientTimeZoneService
{
    Task<TimeZoneInfo> GetClientTimeZoneAsync(CancellationToken cancellationToken);
}
