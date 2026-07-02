using System.Threading.Tasks;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
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
