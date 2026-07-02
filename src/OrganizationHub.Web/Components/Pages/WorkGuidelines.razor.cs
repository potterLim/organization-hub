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

public partial class WorkGuidelines
{
    private List<WorkGuidelineSectionFormModel> mSectionFormModels = new List<WorkGuidelineSectionFormModel>();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<WorkGuidelines>? mDotNetReferenceOrNull;
    private bool mIsEditMode;
    private bool mShouldInitializeSortable;
    private int mSectionCount;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadSectionsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (mIsEditMode == false || mShouldInitializeSortable == false)
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
        moveSection(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.Before));
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        moveSection(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.After));
        await InvokeAsync(StateHasChanged);
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

    private async Task loadSectionsAsync()
    {
        IReadOnlyList<WorkGuidelineSection> sections = await WorkGuidelineService.GetSectionsAsync(CancellationToken.None);
        mSectionFormModels = sections.Select(createFormModel).ToList();
        mSectionCount = mSectionFormModels.Count;
    }

    private void enterEditMode()
    {
        mIsEditMode = true;
        mShouldInitializeSortable = true;
        mMessage = string.Empty;
    }

    private async Task cancelEditAsync()
    {
        mIsEditMode = false;
        await loadSectionsAsync();
    }

    private async Task saveSectionsAsync()
    {
        setSortOrders();
        ServiceResult saveResult = await WorkGuidelineService.SaveSectionsAsync(mSectionFormModels, CancellationToken.None);
        showOperationResult(saveResult, "업무 기준이 저장되었습니다.");
        if (saveResult.IsSuccess == false)
        {
            return;
        }

        mIsEditMode = false;
        await loadSectionsAsync();
    }

    private void setSectionCount(int sectionCount)
    {
        if (WorkGuidelinePolicy.IsValidSectionCount(sectionCount) == false)
        {
            return;
        }

        while (mSectionFormModels.Count < sectionCount)
        {
            mSectionFormModels.Add(createNewSectionFormModel(mSectionFormModels.Count + 1));
        }

        while (mSectionFormModels.Count > sectionCount)
        {
            mSectionFormModels.RemoveAt(mSectionFormModels.Count - 1);
        }

        mSectionCount = sectionCount;
        mShouldInitializeSortable = true;
    }

    private IReadOnlyList<int> getAvailableSectionCounts()
    {
        List<int> sectionCounts = new List<int>();
        for (int sectionCount = WorkGuidelinePolicy.MIN_SECTION_COUNT; sectionCount <= WorkGuidelinePolicy.MAX_SECTION_COUNT; sectionCount++)
        {
            sectionCounts.Add(sectionCount);
        }

        return sectionCounts;
    }

    private string getGuidelineGridClass(IReadOnlyList<WorkGuidelineSectionFormModel> sectionFormModels)
    {
        EWorkGuidelineGridLayout displayLayout = WorkGuidelinePolicy.GetDisplayLayout(sectionFormModels.Count);
        switch (displayLayout)
        {
            case EWorkGuidelineGridLayout.Empty:
                return "hub-rule-grid hub-rule-grid-empty";
            case EWorkGuidelineGridLayout.TwoColumns:
                return "hub-rule-grid hub-rule-grid-two-columns";
            case EWorkGuidelineGridLayout.ThreeColumns:
                return "hub-rule-grid hub-rule-grid-three-columns";
            default:
                throw new InvalidOperationException("Unsupported work guideline grid layout.");
        }
    }

    private IReadOnlyList<string> getGuidelineLines(string body)
    {
        List<string> lines = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => string.IsNullOrWhiteSpace(line) == false)
            .ToList();
        if (lines.Count > 0)
        {
            return lines;
        }

        return new string[] { "내용을 입력해 주세요." };
    }

    private void moveSection(SortableItemMoveRequest moveRequest)
    {
        if (moveRequest.HasRequiredItems == false)
        {
            return;
        }

        WorkGuidelineSectionFormModel? sourceSectionOrNull = mSectionFormModels.FirstOrDefault(section => section.ClientKeyValue == moveRequest.SourceItemKey);
        WorkGuidelineSectionFormModel? targetSectionOrNull = mSectionFormModels.FirstOrDefault(section => section.ClientKeyValue == moveRequest.TargetItemKey);
        if (sourceSectionOrNull == null || targetSectionOrNull == null)
        {
            return;
        }

        mSectionFormModels.Remove(sourceSectionOrNull);
        int targetIndex = mSectionFormModels.IndexOf(targetSectionOrNull);
        if (targetIndex < 0)
        {
            return;
        }

        if (moveRequest.MoveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        mSectionFormModels.Insert(targetIndex, sourceSectionOrNull);
        setSortOrders();
        mShouldInitializeSortable = true;
    }

    private void setSortOrders()
    {
        int sortOrder = WorkGuidelinePolicy.SORT_ORDER_STEP;
        foreach (WorkGuidelineSectionFormModel sectionFormModel in mSectionFormModels)
        {
            sectionFormModel.SortOrder = sortOrder;
            sortOrder += WorkGuidelinePolicy.SORT_ORDER_STEP;
        }
    }

    private WorkGuidelineSectionFormModel createFormModel(WorkGuidelineSection section)
    {
        WorkGuidelineSectionFormModel formModel = new WorkGuidelineSectionFormModel();
        formModel.Id = section.Id;
        formModel.Title = section.Title;
        formModel.Body = section.Body;
        formModel.SortOrder = section.SortOrder;
        return formModel;
    }

    private WorkGuidelineSectionFormModel createNewSectionFormModel(int sectionNumber)
    {
        WorkGuidelineSectionFormModel formModel = new WorkGuidelineSectionFormModel();
        formModel.Title = "새 기준 " + sectionNumber.ToString(CultureInfo.InvariantCulture);
        formModel.Body = "내용을 입력해 주세요.";
        formModel.SortOrder = WorkGuidelinePolicy.GetSortOrderForPosition(sectionNumber);
        return formModel;
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
