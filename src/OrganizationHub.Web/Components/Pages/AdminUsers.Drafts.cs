using System.Text.Json;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private UnsavedChangesGuard? mDraftGuardOrNull;
    private string mSavedFormSnapshot = string.Empty;

    private string createFormSnapshot()
    {
        return JsonSerializer.Serialize(new
        {
            Form = mFormModel,
            IsSystemAdministratorSelected = mIsSystemAdministratorSelected
        });
    }

    private bool hasFormChanges()
    {
        return mSavedFormSnapshot.Length > 0 && (mSavedFormSnapshot != createFormSnapshot() || mResetPassword.Length > 0);
    }

    private Task requestClearFormAsync()
    {
        return requestFormChangeAsync(null);
    }

    private async Task requestFormChangeAsync(UserAccount? userOrNull)
    {
        if (mDraftGuardOrNull != null)
        {
            await mDraftGuardOrNull.RequestChangeAsync(changeFormAsync);
        }
        async Task changeFormAsync()
        {
            if (userOrNull == null)
            {
                clearForm();
            }
            else
            {
                editUser(userOrNull);
            }
            await InvokeAsync(StateHasChanged);
            NavigationManager.NavigateTo("/admin/users#user-editor");
        }
    }
}
