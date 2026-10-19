using System.Linq;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminTeams
{
    private async Task reloadTeamAsync()
    {
        int selectedId = mFormModel.Id;
        await loadDataAsync();
        Team? teamOrNull = mTeams.SingleOrDefault(team => team.Id == selectedId);
        if (teamOrNull == null)
        {
            clearForm();
        }
        else
        {
            editTeam(teamOrNull);
        }
        mHasSaveConflict = false;
        mMessage = string.Empty;
    }
}
