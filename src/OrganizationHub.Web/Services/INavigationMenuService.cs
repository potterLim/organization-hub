using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface INavigationMenuService
{
    Task<IReadOnlyList<NavigationMenuItem>> GetManageableMenuItemsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NavigationMenuItem>> GetVisibleMenuItemsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateMenuItemAsync(NavigationMenuItemFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateMenuItemOrderAsync(NavigationMenuItemOrderUpdateRequest request, CancellationToken cancellationToken);
    Task<bool> CanAccessPathAsync(UserSession userSession, string path, CancellationToken cancellationToken);
}
