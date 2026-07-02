using System.Threading.Tasks;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false)
        {
            return;
        }

        await initializeClientTimeZoneAsync();
    }
}
