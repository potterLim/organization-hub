using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGlobalSettingService
{
    Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken);
    Task<ServiceResult> UpdateGlobalSettingAsync(GlobalSettingFormModel formModel, CancellationToken cancellationToken);
}
