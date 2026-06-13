using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private ApprovalCreateContext mCreateContext = new ApprovalCreateContext();
    private ApprovalRequestFormModel mFormModel = new ApprovalRequestFormModel();
    private List<DriveFolderPathItem> mSourceFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFolderPathItem> mDestinationFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFileItem> mFiles = new List<DriveFileItem>();
    private List<DriveFileItem> mDestinationFoldersInCurrentFolder = new List<DriveFileItem>();
    private string mSelectedDestinationRootFolderID = string.Empty;
    private string mNewDestinationFolderName = string.Empty;
    private string mKeyword = string.Empty;
    private string mMessage = string.Empty;
    private string mFileLoadErrorMessage = string.Empty;
    private string mDestinationFolderLoadErrorMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoading = true;
    private bool mIsLoadingFiles;
    private bool mIsLoadingDestinationFolders;
    private bool mIsCreatingDestinationFolder;
    private bool mIsSubmitting;
    private bool mHasCustomTitle;
    private int mLastDefaultFirstApproverUserID;
    private int mLastDefaultSecondApproverUserID;
    private int mLastDefaultFinalApproverUserID;
    private bool mIsDestinationFolderCreateOpen;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        mCreateContext = await ApprovalService.GetCreateContextAsync(CurrentUserSessionOrNull, CancellationToken.None);
        initializeDefaults();
        if (mCreateContext.SourceFolders.Count > 0)
        {
            await loadFilesAsync();
        }

        if (mCreateContext.DestinationFolders.Count > 0)
        {
            await loadDestinationFoldersAsync();
        }

        mIsLoading = false;
    }

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

    private async Task handleSourceFolderChangedAsync(string folderID)
    {
        mFormModel.SourceFolderID = folderID;
        mFormModel.FileID = string.Empty;
        if (mHasCustomTitle == false)
        {
            mFormModel.Title = string.Empty;
        }

        mKeyword = string.Empty;
        DriveFolderReference? folderOrNull = getSelectedSourceFolderOrNull();
        if (folderOrNull != null)
        {
            resetSourcePath(folderOrNull);
        }

        await loadFilesAsync();
    }

    private async Task handleDestinationRootChangedAsync(string folderID)
    {
        mSelectedDestinationRootFolderID = folderID;
        DriveFolderReference? folderOrNull = getSelectedDestinationRootFolderOrNull();
        if (folderOrNull != null)
        {
            resetDestinationPath(folderOrNull);
        }

        await loadDestinationFoldersAsync();
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

    private async Task loadFilesAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? sourceFolderOrNull = getSelectedSourceFolderOrNull();
        if (sourceFolderOrNull == null)
        {
            return;
        }

        mIsLoadingFiles = true;
        mFileLoadErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, sourceFolderOrNull.FolderKind, sourceFolderOrNull.FolderID, getCurrentSourceFolderID(), mKeyword, CancellationToken.None);
            mFiles = files.ToList();
        }
        catch
        {
            mFiles = new List<DriveFileItem>();
            mFileLoadErrorMessage = "결재할 파일을 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingFiles = false;
        }
    }

    private async Task loadDestinationFoldersAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? destinationFolderOrNull = getSelectedDestinationRootFolderOrNull();
        if (destinationFolderOrNull == null)
        {
            return;
        }

        mIsLoadingDestinationFolders = true;
        mDestinationFolderLoadErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, destinationFolderOrNull.FolderKind, destinationFolderOrNull.FolderID, getCurrentDestinationFolderID(), string.Empty, CancellationToken.None);
            mDestinationFoldersInCurrentFolder = files.Where(file => file.IsFolder).OrderBy(file => file.Name).ToList();
        }
        catch
        {
            mDestinationFoldersInCurrentFolder = new List<DriveFileItem>();
            mDestinationFolderLoadErrorMessage = "저장 위치를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingDestinationFolders = false;
        }
    }

    private async Task openSourceFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = file.ID;
        pathItem.Name = file.Name;
        mSourceFolderPath.Add(pathItem);
        mKeyword = string.Empty;
        await loadFilesAsync();
    }

    private async Task openDestinationFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = file.ID;
        pathItem.Name = file.Name;
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderID = file.ID;
        await loadDestinationFoldersAsync();
    }

    private async Task navigateSourceToPathAsync(int index)
    {
        if (index < 0 || index >= mSourceFolderPath.Count)
        {
            return;
        }

        mSourceFolderPath = mSourceFolderPath.Take(index + 1).ToList();
        mKeyword = string.Empty;
        await loadFilesAsync();
    }

    private async Task navigateDestinationToPathAsync(int index)
    {
        if (index < 0 || index >= mDestinationFolderPath.Count)
        {
            return;
        }

        mDestinationFolderPath = mDestinationFolderPath.Take(index + 1).ToList();
        mFormModel.DestinationFolderID = getCurrentDestinationFolderID();
        await loadDestinationFoldersAsync();
    }

    private async Task createDestinationFolderAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        string folderName = getValidFolderNameOrEmpty(mNewDestinationFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            mMessage = "새 폴더 이름을 입력해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        PermissionDecision createDecision = await PermissionService.DecideStoreApprovedFileToFolderAsync(CurrentUserSessionOrNull, getCurrentDestinationFolderID(), CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            mMessage = createDecision.Reason;
            mMessageSeverity = Severity.Error;
            return;
        }

        mIsCreatingDestinationFolder = true;
        try
        {
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(getCurrentDestinationFolderID(), folderName, CancellationToken.None);
            mNewDestinationFolderName = string.Empty;
            mIsDestinationFolderCreateOpen = false;
            await loadDestinationFoldersAsync();
            await openDestinationFolderAsync(createdFolder);
            mMessage = "새 폴더를 만들었습니다.";
            mMessageSeverity = Severity.Success;
        }
        catch
        {
            mMessage = "폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.";
            mMessageSeverity = Severity.Error;
        }

        mIsCreatingDestinationFolder = false;
    }

    private void toggleDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = mIsDestinationFolderCreateOpen == false;
        if (mIsDestinationFolderCreateOpen == false)
        {
            mNewDestinationFolderName = string.Empty;
        }
    }

    private void closeDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = false;
        mNewDestinationFolderName = string.Empty;
    }

    private async Task handleDestinationFolderCreateKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter" && mIsCreatingDestinationFolder == false)
        {
            await createDestinationFolderAsync();
            return;
        }

        if (keyboardEventArgs.Key == "Escape")
        {
            closeDestinationFolderCreate();
        }
    }

    private void selectFile(DriveFileItem file)
    {
        mFormModel.FileID = file.ID;
        if (mHasCustomTitle == false || string.IsNullOrWhiteSpace(mFormModel.Title))
        {
            mFormModel.Title = createDefaultRequestTitle(file);
            mHasCustomTitle = false;
        }
    }

    private async Task submitRequestAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mFormModel.DestinationFolderID = getCurrentDestinationFolderID();
        mIsSubmitting = true;
        ServiceResult result = await ApprovalService.SubmitRequestAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        mIsSubmitting = false;
        if (result.IsSuccess)
        {
            mMessage = "결재 요청이 등록되었습니다.";
            mMessageSeverity = Severity.Success;
            mFormModel = new ApprovalRequestFormModel();
            mHasCustomTitle = false;
            initializeDefaults();
            await loadFilesAsync();
            await loadDestinationFoldersAsync();
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }

    private DriveFolderReference? getSelectedSourceFolderOrNull()
    {
        return mCreateContext.SourceFolders.FirstOrDefault(folder => folder.FolderID == mFormModel.SourceFolderID);
    }

    private DriveFolderReference? getSelectedDestinationRootFolderOrNull()
    {
        return mCreateContext.DestinationFolders.FirstOrDefault(folder => folder.FolderID == mSelectedDestinationRootFolderID);
    }

    private void resetSourcePath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = folder.FolderID;
        pathItem.Name = folder.DisplayName;
        mSourceFolderPath = new List<DriveFolderPathItem>();
        mSourceFolderPath.Add(pathItem);
    }

    private void resetDestinationPath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = folder.FolderID;
        pathItem.Name = folder.DisplayName;
        mDestinationFolderPath = new List<DriveFolderPathItem>();
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderID = folder.FolderID;
    }

    private string getCurrentSourceFolderID()
    {
        if (mSourceFolderPath.Count == 0)
        {
            return mFormModel.SourceFolderID;
        }

        return mSourceFolderPath[mSourceFolderPath.Count - 1].ID;
    }

    private string getCurrentDestinationFolderID()
    {
        if (mDestinationFolderPath.Count == 0)
        {
            return mSelectedDestinationRootFolderID;
        }

        return mDestinationFolderPath[mDestinationFolderPath.Count - 1].ID;
    }

    private string getValidFolderNameOrEmpty(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        string trimmedName = folderName.Trim();
        if (trimmedName.Length > 80)
        {
            return string.Empty;
        }

        if (trimmedName.Contains("/") || trimmedName.Contains("\\"))
        {
            return string.Empty;
        }

        return trimmedName;
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

    private string getFileCountText()
    {
        int folderCount = mFiles.Count(file => file.IsFolder);
        int fileCount = mFiles.Count - folderCount;
        return "폴더 " + folderCount.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + fileCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string getFileEmptyMessage()
    {
        if (string.IsNullOrWhiteSpace(mKeyword) == false)
        {
            return "검색 결과가 없습니다.";
        }

        return "결재 요청할 파일이 없습니다.";
    }

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return dateTimeOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string createDefaultRequestTitle(DriveFileItem file)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            fileNameWithoutExtension = file.Name;
        }

        return fileNameWithoutExtension + " 결재 요청";
    }

    private string getFileIcon(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return Icons.Material.Filled.Folder;
        }

        if (file.MimeType == "application/pdf")
        {
            return Icons.Material.Filled.PictureAsPdf;
        }

        return Icons.Material.Filled.InsertDriveFile;
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
