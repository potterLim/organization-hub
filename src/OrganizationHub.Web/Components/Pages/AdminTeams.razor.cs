using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminTeams
{
    private const int INITIAL_TEAM_SORT_ORDER = 100;
    private const int TEAM_SORT_ORDER_STEP = 10;

    private List<Team> mTeams = new List<Team>();
    private TeamFormModel mFormModel = new TeamFormModel();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<AdminTeams>? mDotNetReferenceOrNull;
    private bool mShouldInitializeSortable = true;
    private bool mHasSaveConflict;
    private string mFormTitle = "새 팀";
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        clearForm();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (mShouldInitializeSortable == false)
        {
            return;
        }

        mShouldInitializeSortable = false;
        if (mSortableModuleOrNull == null)
        {
            mSortableModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubSortableList.js");
        }

        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        await mSortableModuleOrNull.InvokeVoidAsync("initializeSortableLists", mSortableRootElement, mDotNetReferenceOrNull);
    }

    [JSInvokable("MoveSortableItemBeforeFromScript")]
    public async Task MoveSortableItemBeforeFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveTeamAsync(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.Before));
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveTeamAsync(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.After));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mSortableModuleOrNull != null)
            {
                await mSortableModuleOrNull.InvokeVoidAsync("disposeSortableLists", mSortableRootElement);
                await mSortableModuleOrNull.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (mDotNetReferenceOrNull != null)
        {
            mDotNetReferenceOrNull.Dispose();
        }
    }

    private async Task loadDataAsync()
    {
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mTeams = teams.ToList();
        mShouldInitializeSortable = true;
    }

    private async Task saveTeamAsync()
    {
        ServiceResult saveResult = mFormModel.Id == 0 ? await TeamManagementService.CreateTeamAsync(mFormModel, CancellationToken.None) : await TeamManagementService.UpdateTeamAsync(mFormModel, CancellationToken.None);
        mHasSaveConflict = saveResult.IsConcurrencyConflict;
        showOperationResult(saveResult, "팀 정보가 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            clearForm();
            await loadDataAsync();
        }
    }

    private void editTeam(Team team)
    {
        mFormModel = new TeamFormModel();
        mFormModel.Id = team.Id;
        mFormModel.Revision = team.Revision;
        mFormModel.TeamKey = team.TeamKey;
        mFormModel.DisplayName = team.DisplayName;
        mFormModel.IsActive = team.IsActive;
        mFormModel.SortOrder = team.SortOrder;
        mFormModel.MaterialsFolderId = getTextOrEmpty(team.MaterialsFolderId);
        mFormModel.ApprovedFolderId = getTextOrEmpty(team.ApprovedFolderId);
        mFormModel.CalendarId = getTextOrEmpty(team.CalendarId);
        mFormModel.TaskListId = getTextOrEmpty(team.TaskListId);
        mFormTitle = "팀 수정";
    }

    private async Task toggleTeamActiveAsync(Team team)
    {
        OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(team.Id);
        ServiceResult activationResult = team.IsActive ? await TeamManagementService.DeactivateTeamAsync(teamId, CancellationToken.None) : await TeamManagementService.ActivateTeamAsync(teamId, CancellationToken.None);
        showOperationResult(activationResult, "팀 상태가 변경되었습니다.");
        await loadDataAsync();
    }

    private void clearForm()
    {
        mFormModel = new TeamFormModel();
        mFormModel.IsActive = true;
        mFormModel.SortOrder = getNextSortOrder();
        mFormTitle = "새 팀";
    }

    private async Task moveTeamAsync(SortableItemMoveRequest moveRequest)
    {
        if (moveRequest.HasRequiredItems == false)
        {
            return;
        }

        OrganizationTeamId sourceTeamId = parseOrganizationTeamId(moveRequest.SourceItemKey);
        OrganizationTeamId targetTeamId = parseOrganizationTeamId(moveRequest.TargetItemKey);
        if (sourceTeamId.HasValue == false || targetTeamId.HasValue == false)
        {
            return;
        }

        List<Team> reorderedTeams = moveTeamInList(sourceTeamId, targetTeamId, moveRequest.MoveDirection);
        if (reorderedTeams.Count == 0)
        {
            return;
        }

        IReadOnlyList<OrganizationTeamId> orderedTeamIds = reorderedTeams.Select(team => OrganizationTeamId.CreateExisting(team.Id)).ToList();
        ServiceResult orderResult = await TeamManagementService.UpdateTeamOrderAsync(new OrganizationTeamOrderUpdateRequest(orderedTeamIds, mTeams.ToDictionary(team => team.Id, team => team.Revision)), CancellationToken.None);
        showOperationResult(orderResult, "팀 순서가 저장되었습니다.");
        await loadDataAsync();
        await InvokeAsync(StateHasChanged);
    }

    private List<Team> moveTeamInList(
        OrganizationTeamId sourceTeamId,
        OrganizationTeamId targetTeamId,
        ESortableMoveDirection moveDirection
    )
    {
        List<Team> reorderedTeams = mTeams.ToList();
        Team? sourceTeamOrNull = reorderedTeams.FirstOrDefault(team => team.Id == sourceTeamId.GetRequiredValue());
        Team? targetTeamOrNull = reorderedTeams.FirstOrDefault(team => team.Id == targetTeamId.GetRequiredValue());
        if (sourceTeamOrNull == null || targetTeamOrNull == null)
        {
            return new List<Team>();
        }

        reorderedTeams.Remove(sourceTeamOrNull);
        int targetIndex = reorderedTeams.IndexOf(targetTeamOrNull);
        if (targetIndex < 0)
        {
            return new List<Team>();
        }

        if (moveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        reorderedTeams.Insert(targetIndex, sourceTeamOrNull);
        mTeams = reorderedTeams;
        return reorderedTeams;
    }

    private OrganizationTeamId parseOrganizationTeamId(SortableItemKey itemKey)
    {
        int itemIdValue;
        bool isParsed = int.TryParse(itemKey.GetOptionalValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return OrganizationTeamId.Empty;
        }

        return OrganizationTeamId.CreateExisting(itemIdValue);
    }

    private int getNextSortOrder()
    {
        if (mTeams.Count == 0)
        {
            return INITIAL_TEAM_SORT_ORDER;
        }

        return mTeams.Max(team => team.SortOrder) + TEAM_SORT_ORDER_STEP;
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

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
