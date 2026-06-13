using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private void initializeDefaults()
    {
        if (mCreateContext.SourceFolders.Count > 0)
        {
            mFormModel.SourceFolderID = mCreateContext.SourceFolders[0].FolderID;
            resetSourcePath(mCreateContext.SourceFolders[0]);
        }

        if (mCreateContext.DestinationFolders.Count > 0)
        {
            mSelectedDestinationRootFolderID = mCreateContext.DestinationFolders[0].FolderID;
            resetDestinationPath(mCreateContext.DestinationFolders[0]);
        }

        if (mCreateContext.AvailableStepCounts.Count > 0)
        {
            mFormModel.StepCount = mCreateContext.AvailableStepCounts[0];
        }

        applyDefaultApproversForStepCount();
    }

    private Task handleStepCountChangedAsync(int stepCount)
    {
        mFormModel.StepCount = stepCount;
        applyDefaultApproversForStepCount();
        return Task.CompletedTask;
    }

    private Task handleFirstApproverChangedAsync(int userID)
    {
        mFormModel.FirstApproverUserID = userID;
        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserID == userID)
        {
            mFormModel.SecondApproverUserID = getDefaultSecondApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultSecondApproverUserID = mFormModel.SecondApproverUserID;
        }

        if (mFormModel.FinalApproverUserID == userID)
        {
            mFormModel.FinalApproverUserID = getDefaultFinalApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID);
            mLastDefaultFinalApproverUserID = mFormModel.FinalApproverUserID;
        }

        return Task.CompletedTask;
    }

    private Task handleSecondApproverChangedAsync(int userID)
    {
        mFormModel.SecondApproverUserID = userID;
        if (mFormModel.FirstApproverUserID == userID)
        {
            mFormModel.FirstApproverUserID = getDefaultFirstApproverIDExcluding(mFormModel.SecondApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultFirstApproverUserID = mFormModel.FirstApproverUserID;
        }

        if (mFormModel.FinalApproverUserID == userID)
        {
            mFormModel.FinalApproverUserID = getDefaultFinalApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID);
            mLastDefaultFinalApproverUserID = mFormModel.FinalApproverUserID;
        }

        return Task.CompletedTask;
    }

    private Task handleFinalApproverChangedAsync(int userID)
    {
        mFormModel.FinalApproverUserID = userID;
        if (mFormModel.FirstApproverUserID == userID)
        {
            mFormModel.FirstApproverUserID = getDefaultFirstApproverIDExcluding(mFormModel.SecondApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultFirstApproverUserID = mFormModel.FirstApproverUserID;
        }

        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserID == userID)
        {
            mFormModel.SecondApproverUserID = getDefaultSecondApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultSecondApproverUserID = mFormModel.SecondApproverUserID;
        }

        return Task.CompletedTask;
    }

    private Task handleTitleChangedAsync(string title)
    {
        mFormModel.Title = title;
        mHasCustomTitle = true;
        return Task.CompletedTask;
    }

    private void applyDefaultApproversForStepCount()
    {
        if (mFormModel.StepCount < 2)
        {
            mFormModel.FirstApproverUserID = 0;
            mLastDefaultFirstApproverUserID = 0;
        }

        if (mFormModel.StepCount < 3)
        {
            mFormModel.SecondApproverUserID = 0;
            mLastDefaultSecondApproverUserID = 0;
        }

        if (mFormModel.StepCount >= 2 && shouldApplyDefaultApprover(mFormModel.FirstApproverUserID, mLastDefaultFirstApproverUserID, mFormModel.SecondApproverUserID, mFormModel.FinalApproverUserID))
        {
            mFormModel.FirstApproverUserID = getDefaultFirstApproverIDExcluding(mFormModel.SecondApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultFirstApproverUserID = mFormModel.FirstApproverUserID;
        }

        if (mFormModel.StepCount == 3 && shouldApplyDefaultApprover(mFormModel.SecondApproverUserID, mLastDefaultSecondApproverUserID, mFormModel.FirstApproverUserID, mFormModel.FinalApproverUserID))
        {
            mFormModel.SecondApproverUserID = getDefaultSecondApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.FinalApproverUserID);
            mLastDefaultSecondApproverUserID = mFormModel.SecondApproverUserID;
        }

        if (shouldApplyDefaultFinalApprover(mFormModel.FinalApproverUserID, mLastDefaultFinalApproverUserID, mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID))
        {
            mFormModel.FinalApproverUserID = getDefaultFinalApproverIDExcluding(mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID);
            mLastDefaultFinalApproverUserID = mFormModel.FinalApproverUserID;
        }
    }

    private IReadOnlyList<ApprovalUserOption> getFirstApproverOptions()
    {
        return getApproverOptionsExcludingExcept(mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID, mFormModel.FinalApproverUserID);
    }

    private IReadOnlyList<ApprovalUserOption> getSecondApproverOptions()
    {
        return getApproverOptionsExcludingExcept(mFormModel.SecondApproverUserID, mFormModel.FirstApproverUserID, mFormModel.FinalApproverUserID);
    }

    private IReadOnlyList<ApprovalUserOption> getFinalApproverOptions()
    {
        return getFinalApproverOptionsExcludingExcept(mFormModel.FinalApproverUserID, mFormModel.FirstApproverUserID, mFormModel.SecondApproverUserID);
    }

    private IReadOnlyList<ApprovalUserOption> getApproverOptionsExcludingExcept(int currentUserID, params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0 && userID != currentUserID).ToHashSet();
        return mCreateContext.ApproverOptions.Where(userOption => excludedIDs.Contains(userOption.UserID) == false).ToList();
    }

    private IReadOnlyList<ApprovalUserOption> getFinalApproverOptionsExcludingExcept(int currentUserID, params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0 && userID != currentUserID).ToHashSet();
        return mCreateContext.FinalApproverOptions.Where(userOption => excludedIDs.Contains(userOption.UserID) == false).ToList();
    }

    private int getFirstApproverIDExcluding(params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0).ToHashSet();
        ApprovalUserOption? approverOptionOrNull = mCreateContext.ApproverOptions.FirstOrDefault(userOption => excludedIDs.Contains(userOption.UserID) == false);
        if (approverOptionOrNull == null)
        {
            return 0;
        }

        return approverOptionOrNull.UserID;
    }

    private int getFirstFinalApproverIDExcluding(params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0).ToHashSet();
        ApprovalUserOption? finalApproverOptionOrNull = mCreateContext.FinalApproverOptions.FirstOrDefault(userOption => excludedIDs.Contains(userOption.UserID) == false);
        if (finalApproverOptionOrNull == null)
        {
            return 0;
        }

        return finalApproverOptionOrNull.UserID;
    }

    private int getDefaultFirstApproverIDExcluding(params int[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        int preferredUserID = defaultLineOrNull == null ? 0 : defaultLineOrNull.FirstApproverUserID;
        return getPreferredApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private int getDefaultSecondApproverIDExcluding(params int[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        int preferredUserID = defaultLineOrNull == null ? 0 : defaultLineOrNull.SecondApproverUserID;
        return getPreferredApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private int getDefaultFinalApproverIDExcluding(params int[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        int preferredUserID = defaultLineOrNull == null ? 0 : defaultLineOrNull.FinalApproverUserID;
        return getPreferredFinalApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private int getPreferredApproverIDExcluding(int preferredUserID, params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0).ToHashSet();
        if (preferredUserID > 0 && excludedIDs.Contains(preferredUserID) == false && isApproverOptionSelected(preferredUserID))
        {
            return preferredUserID;
        }

        return getFirstApproverIDExcluding(excludedUserIDs);
    }

    private int getPreferredFinalApproverIDExcluding(int preferredUserID, params int[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = excludedUserIDs.Where(userID => userID > 0).ToHashSet();
        if (preferredUserID > 0 && excludedIDs.Contains(preferredUserID) == false && isFinalApproverOptionSelected(preferredUserID))
        {
            return preferredUserID;
        }

        return getFirstFinalApproverIDExcluding(excludedUserIDs);
    }

    private ApprovalDefaultLineOption? getDefaultLineOrNull()
    {
        return mCreateContext.DefaultLineOptions.FirstOrDefault(defaultLine => defaultLine.StepCount == mFormModel.StepCount);
    }

    private bool shouldApplyDefaultApprover(int selectedUserID, int lastDefaultUserID, params int[] excludedUserIDs)
    {
        if (selectedUserID <= 0 || isApproverOptionSelected(selectedUserID) == false)
        {
            return true;
        }

        if (excludedUserIDs.Any(userID => userID > 0 && userID == selectedUserID))
        {
            return true;
        }

        return selectedUserID == lastDefaultUserID;
    }

    private bool shouldApplyDefaultFinalApprover(int selectedUserID, int lastDefaultUserID, params int[] excludedUserIDs)
    {
        if (selectedUserID <= 0 || isFinalApproverOptionSelected(selectedUserID) == false)
        {
            return true;
        }

        if (excludedUserIDs.Any(userID => userID > 0 && userID == selectedUserID))
        {
            return true;
        }

        return selectedUserID == lastDefaultUserID;
    }

    private bool isApproverOptionSelected(int userID)
    {
        return userID > 0 && mCreateContext.ApproverOptions.Any(userOption => userOption.UserID == userID);
    }

    private bool isFinalApproverOptionSelected(int userID)
    {
        return userID > 0 && mCreateContext.FinalApproverOptions.Any(userOption => userOption.UserID == userID);
    }

    private string getApprovalStepLabel(int stepCount)
    {
        if (stepCount == 2)
        {
            return "1차 결재 + 최종 결재";
        }

        if (stepCount == 3)
        {
            return "1차 결재 + 2차 결재 + 최종 결재";
        }

        return stepCount.ToString(CultureInfo.InvariantCulture) + "단계";
    }

    private string getApprovalStepDescription()
    {
        if (mFormModel.StepCount == 3)
        {
            return "중간 결재자를 한 명 더 거친 뒤 최종 결재를 받습니다.";
        }

        return "1차 결재 후 최종 결재를 받습니다.";
    }

    private IReadOnlyList<ApprovalFlowPreviewItem> getApprovalFlowPreviewItems()
    {
        List<ApprovalFlowPreviewItem> previewItems = new List<ApprovalFlowPreviewItem>();
        if (mFormModel.StepCount >= 2)
        {
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "1차 결재", mFormModel.FirstApproverUserID));
        }

        if (mFormModel.StepCount == 3)
        {
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "2차 결재", mFormModel.SecondApproverUserID));
        }

        previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "최종 결재", mFormModel.FinalApproverUserID));
        return previewItems;
    }

    private ApprovalFlowPreviewItem createApprovalFlowPreviewItem(int stepNumber, string title, int approverUserID)
    {
        ApprovalFlowPreviewItem previewItem = new ApprovalFlowPreviewItem();
        previewItem.StepNumber = stepNumber.ToString(CultureInfo.InvariantCulture);
        previewItem.Title = title;
        previewItem.ApproverLabel = getApproverLabelOrPlaceholder(approverUserID);
        previewItem.IsComplete = approverUserID > 0;
        return previewItem;
    }

    private string getApproverLabelOrPlaceholder(int approverUserID)
    {
        if (approverUserID <= 0)
        {
            return "선택 필요";
        }

        ApprovalUserOption? userOptionOrNull = mCreateContext.ApproverOptions
            .Concat(mCreateContext.FinalApproverOptions)
            .FirstOrDefault(userOption => userOption.UserID == approverUserID);
        if (userOptionOrNull == null)
        {
            return "선택 필요";
        }

        return userOptionOrNull.GetDisplayLabel();
    }

    private string getApprovalFlowStepClass(ApprovalFlowPreviewItem previewItem)
    {
        if (previewItem.IsComplete)
        {
            return "hub-approval-flow-step";
        }

        return "hub-approval-flow-step hub-approval-flow-step-empty";
    }

    private sealed class ApprovalFlowPreviewItem
    {
        public string StepNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ApproverLabel { get; set; } = string.Empty;
        public bool IsComplete
        {
            get; set;
        }
    }
}
