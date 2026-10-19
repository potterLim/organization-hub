using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGlobalSettingService
{
    Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken);
    Task<ServiceResult> UpdateDisplayOptionsAsync(UserSession userSession, GlobalSettingFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> AssignFileAsync(UserSession userSession, SettingFileAssignmentCommand command, CancellationToken cancellationToken);
}
