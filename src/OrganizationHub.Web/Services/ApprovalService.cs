using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService : IApprovalService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly IDriveBrowserService? mDriveBrowserServiceOrNull;
    private readonly IChangeVersionService? mChangeVersionServiceOrNull;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public ApprovalService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPermissionService permissionService, IDriveService driveService, IDriveBrowserService? driveBrowserServiceOrNull = null, IChangeVersionService? changeVersionServiceOrNull = null)
        : this(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory), driveBrowserServiceOrNull, changeVersionServiceOrNull)
    {
    }

    public ApprovalService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPermissionService permissionService, IDriveService driveService, IOrganizationPermissionService organizationPermissionService, IDriveBrowserService? driveBrowserServiceOrNull = null, IChangeVersionService? changeVersionServiceOrNull = null)
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
        mOrganizationPermissionService = organizationPermissionService;
        mDriveBrowserServiceOrNull = driveBrowserServiceOrNull;
        mChangeVersionServiceOrNull = changeVersionServiceOrNull;
    }

    public async Task<ApprovalCreateContext> GetCreateContextAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        ApprovalCreateContext context = new ApprovalCreateContext();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        bool canCreateApprovalRequest = usesConfiguredPermissions ? await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.APPROVAL_REQUEST, userSession.TeamID, cancellationToken) : UserPermissionPolicy.CanCreateApprovalRequests(userSession.Role);
        if (canCreateApprovalRequest == false)
        {
            context.BlockReason = "결재 요청 권한이 없습니다.";
            return context;
        }

        IReadOnlyList<DriveFolderReference> readableFolders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, cancellationToken);
        List<DriveFolderReference> sourceFolders = createApprovalSourceFolders(userSession, readableFolders);
        context.SourceFolders = sourceFolders;
        context.DestinationFolders = await mPermissionService.GetApprovalDestinationDriveFoldersAsync(userSession, cancellationToken);
        context.UsesDefaultFinalApprover = usesConfiguredPermissions == false && UserPermissionPolicy.UsesDefaultFinalApproval(userSession.Role);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UserAccount> users = await dbContext.UserAccounts
                .Include(user => user.Team)
                .Include(user => user.Position)
                .ThenInclude(position => position!.PermissionGrants)
                .Where(user => user.IsActive)
                .OrderBy(user => user.DisplayName)
                .ToListAsync(cancellationToken);
            ConfiguredApprovalLine? configuredLineOrNull = await createConfiguredApprovalLineOrNullAsync(dbContext, users, userSession, cancellationToken);
            if (configuredLineOrNull != null)
            {
                context.ApproverOptions = configuredLineOrNull.IntermediateApprovers;
                context.FinalApproverOptions = configuredLineOrNull.FinalApprovers;
                context.FinalApproverCandidates = configuredLineOrNull.FinalApprovers;
                context.AvailableStepCounts = new List<int> { configuredLineOrNull.StepCount };
                context.DefaultLineOptions = new List<ApprovalDefaultLineOption> { configuredLineOrNull.DefaultLine };
                context.CanCreateRequest = sourceFolders.Count > 0 && context.DestinationFolders.Count > 0 && context.FinalApproverOptions.Count > 0;
                if (context.CanCreateRequest == false)
                {
                    context.BlockReason = "결재 요청을 만들 수 없습니다.";
                }

                return context;
            }

            context.ApproverOptions = createApproverOptions(users, userSession);
            context.FinalApproverCandidates = createFinalApproverOptions(users);
            context.FinalApproverOptions = createFinalApproverOptions(users, userSession);
        }

        context.AvailableStepCounts = createAvailableStepCounts(userSession, context.ApproverOptions, context.FinalApproverOptions);
        context.DefaultLineOptions = ApprovalDefaultLinePolicy.CreateDefaultLineOptions(userSession, context.ApproverOptions, context.FinalApproverOptions, context.AvailableStepCounts);
        context.CanCreateRequest = sourceFolders.Count > 0 && context.DestinationFolders.Count > 0 && context.FinalApproverOptions.Count > 0 && context.AvailableStepCounts.Count > 0;
        if (context.CanCreateRequest == false)
        {
            context.BlockReason = "결재 요청을 만들 수 없습니다.";
        }

        return context;
    }

    public async Task<ServiceResult> SubmitRequestAsync(UserSession userSession, ApprovalRequestFormModel formModel, CancellationToken cancellationToken)
    {
        ApprovalCreateContext context = await GetCreateContextAsync(userSession, cancellationToken);
        if (context.CanCreateRequest == false)
        {
            return ServiceResult.Fail(context.BlockReason);
        }

        ServiceResult validationResult = validateSubmitForm(formModel, context);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        DriveFolderReference? sourceFolderOrNull = context.SourceFolders.FirstOrDefault(folder => folder.FolderID == formModel.SourceFolderID);
        if (sourceFolderOrNull == null)
        {
            return ServiceResult.Fail("결재 요청을 만들 수 없습니다.");
        }

        DriveFolderReference? destinationRootFolderOrNull = context.DestinationFolders.FirstOrDefault(folder => folder.FolderID == formModel.DestinationFolderID);
        if (destinationRootFolderOrNull == null)
        {
            bool canStoreApprovedFile = await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, formModel.DestinationFolderID, cancellationToken);
            if (canStoreApprovedFile == false)
            {
                return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
            }
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(formModel.FileID, cancellationToken);
        if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == "application/vnd.google-apps.folder")
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        DriveFileMetadata? destinationFolderMetadataOrNull = await mDriveService.GetFileOrNullAsync(formModel.DestinationFolderID, cancellationToken);
        if (destinationFolderMetadataOrNull == null || destinationFolderMetadataOrNull.MimeType != "application/vnd.google-apps.folder")
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        bool isFileInsideSourceFolder = await isFileInsideFolderAsync(fileMetadataOrNull, sourceFolderOrNull.FolderID, cancellationToken);
        if (isFileInsideSourceFolder == false)
        {
            return ServiceResult.Fail("선택한 파일을 결재 요청 위치에서 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalUserOption? firstApproverOrNull = getFirstApproverOrNull(context.ApproverOptions, formModel);
            ApprovalUserOption? secondApproverOrNull = getSecondApproverOrNull(context.ApproverOptions, formModel);
            ApprovalUserOption finalApprover = getRequiredUserOption(context.FinalApproverOptions, resolveFinalApproverUserID(context.FinalApproverOptions, formModel.FinalApproverUserID));
            ApprovalRequest request = createApprovalRequest(userSession, formModel, sourceFolderOrNull, fileMetadataOrNull, destinationFolderMetadataOrNull);
            if (formModel.StepCount >= 2 && firstApproverOrNull != null)
            {
                request.Steps.Add(createApprovalStep(1, firstApproverOrNull));
            }

            if (formModel.StepCount == 3 && secondApproverOrNull != null)
            {
                request.Steps.Add(createApprovalStep(2, secondApproverOrNull));
            }

            request.Steps.Add(createApprovalStep(formModel.StepCount, finalApprover));
            request.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.Submitted, null, "결재 요청", string.Empty, string.Empty));
            dbContext.ApprovalRequests.Add(request);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetPendingApprovalsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<ApprovalRequest> requests = await dbContext.ApprovalRequests.Include(request => request.Steps).Include(request => request.AuditLogs).AsSplitQuery().Where(request => request.Status == EApprovalRequestStatus.Pending && request.Steps.Any(step => step.StepNumber == request.CurrentStepNumber && step.ApproverUserID == userSession.UserID && step.Status == EApprovalStepStatus.Pending)).OrderBy(request => request.RequestedAt).ToListAsync(cancellationToken);
            return createApprovalRequestItems(requests);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetRelatedHistoryAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<ApprovalRequest> query = dbContext.ApprovalRequests.Include(request => request.Steps).Include(request => request.AuditLogs).AsSplitQuery().Where(request => request.Status != EApprovalRequestStatus.Pending);
            if (userSession.Role != EUserRole.Admin)
            {
                int userID = userSession.UserID;
                query = query.Where(request => request.RequesterUserID == userID || request.Steps.Any(step => step.ApproverUserID == userID && step.Status != EApprovalStepStatus.Pending));
            }

            List<ApprovalRequest> requests = await query.OrderByDescending(request => request.RequestedAt).Take(100).ToListAsync(cancellationToken);
            return createApprovalRequestItems(requests);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetApprovedRequestsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<ApprovalRequest> query = dbContext.ApprovalRequests.Include(request => request.Steps).Include(request => request.AuditLogs).AsSplitQuery().Where(request => request.Status == EApprovalRequestStatus.Approved);
            if (userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    return new List<ApprovalRequestItem>();
                }

                int teamID = userSession.TeamID.Value;
                query = query.Where(request => request.TeamID == teamID);
            }

            List<ApprovalRequest> requests = await query.OrderByDescending(request => request.CompletedAt).ThenByDescending(request => request.RequestedAt).ToListAsync(cancellationToken);
            List<ApprovalRequest> readableRequests = await filterApprovedRequestsByDestinationAsync(userSession, requests, cancellationToken);
            return createApprovalRequestItems(readableRequests);
        }
    }

    public async Task<ServiceResult> ApproveRequestAsync(UserSession userSession, int approvalRequestID, int nextApproverUserID, string comment, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, approvalRequestID, cancellationToken);
            if (requestOrNull == null)
            {
                return ServiceResult.Fail("결재 요청을 찾을 수 없습니다.");
            }

            ApprovalStep? currentStepOrNull = getCurrentStepOrNull(requestOrNull, userSession);
            if (currentStepOrNull == null)
            {
                return ServiceResult.Fail("결재할 권한이 없습니다.");
            }

            ServiceResult changeResult = await changeNextApproverIfRequestedAsync(dbContext, requestOrNull, userSession, nextApproverUserID, cancellationToken);
            if (changeResult.IsSuccess == false)
            {
                return changeResult;
            }

            if (currentStepOrNull.StepNumber == requestOrNull.StepCount)
            {
                ServiceResult copyResult = await copyApprovedFileIfConfiguredAsync(dbContext, requestOrNull, cancellationToken);
                if (copyResult.IsSuccess == false)
                {
                    return copyResult;
                }
            }

            currentStepOrNull.Status = EApprovalStepStatus.Approved;
            currentStepOrNull.ActionedAt = DateTime.UtcNow;
            currentStepOrNull.Comment = getTrimmedComment(comment);
            requestOrNull.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.Approved, currentStepOrNull.StepNumber, currentStepOrNull.Comment, string.Empty, string.Empty));
            if (currentStepOrNull.StepNumber == requestOrNull.StepCount)
            {
                requestOrNull.Status = EApprovalRequestStatus.Approved;
                requestOrNull.CompletedAt = DateTime.UtcNow;
                requestOrNull.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.Completed, currentStepOrNull.StepNumber, "최종 결재 완료", string.Empty, string.Empty));
            }
            else
            {
                requestOrNull.CurrentStepNumber = currentStepOrNull.StepNumber + 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> RejectRequestAsync(UserSession userSession, int approvalRequestID, string comment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return ServiceResult.Fail("반려 코멘트를 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, approvalRequestID, cancellationToken);
            if (requestOrNull == null)
            {
                return ServiceResult.Fail("결재 요청을 찾을 수 없습니다.");
            }

            ApprovalStep? currentStepOrNull = getCurrentStepOrNull(requestOrNull, userSession);
            if (currentStepOrNull == null)
            {
                return ServiceResult.Fail("반려할 권한이 없습니다.");
            }

            currentStepOrNull.Status = EApprovalStepStatus.Rejected;
            currentStepOrNull.ActionedAt = DateTime.UtcNow;
            currentStepOrNull.Comment = getTrimmedComment(comment);
            requestOrNull.Status = EApprovalRequestStatus.Rejected;
            requestOrNull.RejectedAt = DateTime.UtcNow;
            requestOrNull.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.Rejected, currentStepOrNull.StepNumber, currentStepOrNull.Comment, string.Empty, string.Empty));
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

}
