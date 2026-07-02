using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private async Task saveUserAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        syncFormAccountKindAndTeam();
        ServiceResult saveResult;
        if (mFormModel.Id == 0)
        {
            saveResult = await UserManagementService.CreateUserAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        }
        else
        {
            saveResult = await UserManagementService.UpdateUserAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        }

        showOperationResult(saveResult, "사용자 정보가 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            clearForm();
            await loadDataAsync();
        }
    }

    private async Task toggleUserActiveAsync(UserAccount userAccount)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        UserAccountId userAccountId = UserAccountId.CreateExisting(userAccount.Id);
        ServiceResult activationResult;
        if (userAccount.IsActive)
        {
            activationResult = await UserManagementService.DeactivateUserAsync(CurrentUserSessionOrNull, userAccountId, CancellationToken.None);
        }
        else
        {
            activationResult = await UserManagementService.ActivateUserAsync(CurrentUserSessionOrNull, userAccountId, CancellationToken.None);
        }

        showOperationResult(activationResult, "사용자 상태가 변경되었습니다.");
        await loadDataAsync();
    }

    private async Task resetPasswordAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        UserAccountId userAccountId = UserAccountId.CreateExisting(mFormModel.Id);
        ServiceResult passwordResetResult = await UserManagementService.ResetPasswordAsync(
            CurrentUserSessionOrNull,
            userAccountId,
            PlainPassword.CreateFromInput(mResetPassword),
            CancellationToken.None);
        showOperationResult(passwordResetResult, "비밀번호가 초기화되었습니다.");
        if (passwordResetResult.IsSuccess)
        {
            mResetPassword = string.Empty;
        }
    }
}
