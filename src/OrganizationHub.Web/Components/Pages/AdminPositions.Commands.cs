using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private async Task createPositionAsync()
    {
        int createdTeamId = mNewPositionFormModel.TeamId;
        string createdPositionName = mNewPositionFormModel.Name.Trim();
        ServiceResult creationResult = await PositionManagementService.CreatePositionAsync(mNewPositionFormModel, CancellationToken.None);
        showOperationResult(creationResult, "직책이 만들어졌습니다. 권한 설정에서 필요한 업무 권한을 선택해 주세요.");
        if (creationResult.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPermissionTeamId = createdTeamId;
        OrganizationPosition? createdPositionOrNull = mPositions.FirstOrDefault(position => position.TeamId == createdTeamId && position.Name == createdPositionName);
        mSelectedPositionIdOrNull = createdPositionOrNull == null ? null : createdPositionOrNull.Id;
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
        mNewPositionFormModel = createEmptyFormModel(createdTeamId);
    }

    private async Task savePermissionAsync()
    {
        if (mSelectedPositionIdOrNull.HasValue == false)
        {
            mMessage = "권한을 설정할 직책을 선택해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        int selectedPositionId = mSelectedPositionIdOrNull.Value;
        ServiceResult permissionSaveResult = await PositionManagementService.UpdatePositionAsync(mPermissionFormModel, CancellationToken.None);
        showOperationResult(permissionSaveResult, "직책 정보와 권한이 저장되었습니다.");
        mHasSaveConflict = permissionSaveResult.IsConcurrencyConflict;
        if (permissionSaveResult.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPositionIdOrNull = selectedPositionId;
        ensurePermissionTeamSelection();
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private async Task togglePositionActiveAsync(OrganizationPosition position)
    {
        ServiceResult activationResult;
        if (position.IsActive)
        {
            activationResult = await PositionManagementService.DeactivatePositionAsync(OrganizationPositionId.CreateExisting(position.Id), CancellationToken.None);
        }
        else
        {
            activationResult = await PositionManagementService.ActivatePositionAsync(OrganizationPositionId.CreateExisting(position.Id), CancellationToken.None);
        }

        showOperationResult(activationResult, "직책 상태가 변경되었습니다.");
        await loadDataAsync();
        if (mSelectedPositionIdOrNull == position.Id)
        {
            loadSelectedPositionForm();
        }
    }

    private async Task reloadPositionAsync()
    {
        await loadDataAsync();
        ensurePermissionTeamSelection();
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
        mHasSaveConflict = false;
        mMessage = string.Empty;
    }

    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
