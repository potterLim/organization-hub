using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private async Task createPositionAsync()
    {
        int createdTeamID = mNewPositionFormModel.TeamID;
        string createdPositionName = mNewPositionFormModel.Name.Trim();
        ServiceResult result = await PositionManagementService.CreatePositionAsync(mNewPositionFormModel, CancellationToken.None);
        showResult(result, "직책이 만들어졌습니다. 오른쪽에서 권한을 설정할 수 있습니다.");
        if (result.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPermissionTeamID = createdTeamID;
        OrganizationPosition? createdPositionOrNull = mPositions.FirstOrDefault(position => position.TeamID == createdTeamID && position.Name == createdPositionName);
        mSelectedPositionIDOrNull = createdPositionOrNull == null ? null : createdPositionOrNull.ID;
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
        mNewPositionFormModel = createEmptyFormModel(createdTeamID);
    }

    private async Task savePermissionAsync()
    {
        if (mSelectedPositionIDOrNull.HasValue == false)
        {
            mMessage = "권한을 설정할 직책을 선택해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        int selectedPositionID = mSelectedPositionIDOrNull.Value;
        ServiceResult result = await PositionManagementService.UpdatePositionAsync(mPermissionFormModel, CancellationToken.None);
        showResult(result, "직책 권한이 저장되었습니다.");
        if (result.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPositionIDOrNull = selectedPositionID;
        ensurePermissionTeamSelection();
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private async Task togglePositionActiveAsync(OrganizationPosition position)
    {
        ServiceResult result;
        if (position.IsActive)
        {
            result = await PositionManagementService.DeactivatePositionAsync(position.ID, CancellationToken.None);
        }
        else
        {
            result = await PositionManagementService.ActivatePositionAsync(position.ID, CancellationToken.None);
        }

        showResult(result, "직책 상태가 변경되었습니다.");
        await loadDataAsync();
        if (mSelectedPositionIDOrNull == position.ID)
        {
            loadSelectedPositionForm();
        }
    }

    private void showResult(ServiceResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
