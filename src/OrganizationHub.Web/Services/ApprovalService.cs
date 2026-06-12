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

public sealed class ApprovalService : IApprovalService
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

    private async Task<ConfiguredApprovalLine?> createConfiguredApprovalLineOrNullAsync(OrganizationHubDbContext dbContext, IReadOnlyList<UserAccount> users, UserSession userSession, CancellationToken cancellationToken)
    {
        ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows
            .Include(workflow => workflow.Steps)
            .Where(workflow => workflow.IsActive && (workflow.AppliesToTeamID == null || workflow.AppliesToTeamID == userSession.TeamID))
            .OrderBy(workflow => workflow.AppliesToTeamID == userSession.TeamID ? 0 : 1)
            .ThenBy(workflow => workflow.Priority)
            .FirstOrDefaultAsync(cancellationToken);
        if (workflowOrNull == null)
        {
            return null;
        }

        List<ApprovalWorkflowStep> steps = workflowOrNull.Steps.OrderBy(step => step.StepOrder).ToList();
        if (steps.Count <= 0 || steps.Count > 3)
        {
            return null;
        }

        List<ApprovalUserOption> selectedApprovers = new List<ApprovalUserOption>();
        HashSet<int> usedUserIDs = new HashSet<int>();
        foreach (ApprovalWorkflowStep step in steps)
        {
            bool isFinalStep = step.StepOrder == steps[steps.Count - 1].StepOrder;
            UserAccount? approverOrNull = resolveWorkflowStepApproverOrNull(users, userSession, step, usedUserIDs, isFinalStep);
            if (approverOrNull == null)
            {
                if (step.IsRequired)
                {
                    return null;
                }

                continue;
            }

            usedUserIDs.Add(approverOrNull.ID);
            selectedApprovers.Add(createApprovalUserOption(approverOrNull));
        }

        if (selectedApprovers.Count <= 0)
        {
            return null;
        }

        ApprovalUserOption finalApprover = selectedApprovers[selectedApprovers.Count - 1];
        List<ApprovalUserOption> intermediateApprovers = selectedApprovers.Take(selectedApprovers.Count - 1).ToList();
        ApprovalDefaultLineOption defaultLine = new ApprovalDefaultLineOption();
        defaultLine.StepCount = selectedApprovers.Count;
        defaultLine.FinalApproverUserID = finalApprover.UserID;
        if (selectedApprovers.Count >= 2)
        {
            defaultLine.FirstApproverUserID = selectedApprovers[0].UserID;
        }

        if (selectedApprovers.Count == 3)
        {
            defaultLine.SecondApproverUserID = selectedApprovers[1].UserID;
        }

        return new ConfiguredApprovalLine(selectedApprovers.Count, intermediateApprovers, new List<ApprovalUserOption> { finalApprover }, defaultLine);
    }

    private UserAccount? resolveWorkflowStepApproverOrNull(IReadOnlyList<UserAccount> users, UserSession userSession, ApprovalWorkflowStep step, IReadOnlySet<int> usedUserIDs, bool isFinalStep)
    {
        List<UserAccount> candidates = getWorkflowStepCandidates(users, userSession, step, isFinalStep)
            .Where(user => user.ID != userSession.UserID && usedUserIDs.Contains(user.ID) == false)
            .OrderBy(user => getApproverSortOrder(user, userSession))
            .ThenBy(user => user.DisplayName)
            .ToList();
        return candidates.FirstOrDefault();
    }

    private List<UserAccount> getWorkflowStepCandidates(IReadOnlyList<UserAccount> users, UserSession userSession, ApprovalWorkflowStep step, bool isFinalStep)
    {
        switch (step.ApproverSelectionType)
        {
            case EApproverSelectionType.SpecificUser:
                return users.Where(user => step.ApproverUserID.HasValue && user.ID == step.ApproverUserID.Value).ToList();
            case EApproverSelectionType.SpecificPosition:
                return users.Where(user => step.ApproverPositionID.HasValue && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PositionInRequesterTeam:
                return users.Where(user => userSession.TeamID.HasValue && step.ApproverPositionID.HasValue && user.TeamID == userSession.TeamID.Value && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PositionInSelectedTeam:
                return users.Where(user => step.ApproverTeamID.HasValue && step.ApproverPositionID.HasValue && user.TeamID == step.ApproverTeamID.Value && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PermissionHolder:
                return users.Where(user => userHasWorkflowPermission(user, userSession, step, isFinalStep)).ToList();
            default:
                return new List<UserAccount>();
        }
    }

    private bool userHasWorkflowPermission(UserAccount user, UserSession userSession, ApprovalWorkflowStep step, bool isFinalStep)
    {
        if (user.Position == null || user.Position.IsActive == false)
        {
            return false;
        }

        string permissionKey = getWorkflowStepPermissionKey(step, isFinalStep);
        foreach (PositionPermissionGrant permissionGrant in user.Position.PermissionGrants)
        {
            if (permissionGrant.IsAllowed == false || permissionGrant.PermissionKey != permissionKey)
            {
                continue;
            }

            if (isWorkflowPermissionGrantInScope(permissionGrant, user, userSession.TeamID))
            {
                return true;
            }
        }

        return false;
    }

    private string getWorkflowStepPermissionKey(ApprovalWorkflowStep step, bool isFinalStep)
    {
        if (string.IsNullOrWhiteSpace(step.RequiredPermissionKey) == false)
        {
            return step.RequiredPermissionKey;
        }

        if (isFinalStep)
        {
            return PermissionKeys.APPROVAL_FINAL_APPROVE;
        }

        return PermissionKeys.APPROVAL_APPROVE;
    }

    private bool isWorkflowPermissionGrantInScope(PositionPermissionGrant permissionGrant, UserAccount user, int? requesterTeamIDOrNull)
    {
        switch (permissionGrant.ScopeType)
        {
            case EPermissionScopeType.Global:
                return true;
            case EPermissionScopeType.OwnTeam:
            case EPermissionScopeType.OwnTeamAndChildren:
                return requesterTeamIDOrNull.HasValue && user.TeamID.HasValue && requesterTeamIDOrNull.Value == user.TeamID.Value;
            case EPermissionScopeType.SelectedTeam:
                return requesterTeamIDOrNull.HasValue && permissionGrant.TargetTeamID.HasValue && requesterTeamIDOrNull.Value == permissionGrant.TargetTeamID.Value;
            default:
                return false;
        }
    }

    private List<ApprovalUserOption> createApproverOptions(IReadOnlyList<UserAccount> users, UserSession userSession)
    {
        List<ApprovalUserOption> options = new List<ApprovalUserOption>();
        List<UserAccount> approverUsers = users.Where(user => user.ID != userSession.UserID && UserPermissionPolicy.CanUseAsIntermediateApprovalApprover(userSession.Role, user.Role)).OrderBy(user => getApproverSortOrder(user, userSession)).ThenBy(user => user.DisplayName).ToList();
        foreach (UserAccount user in approverUsers)
        {
            options.Add(createApprovalUserOption(user));
        }

        return options;
    }

    private List<DriveFolderReference> createApprovalSourceFolders(UserSession userSession, IReadOnlyList<DriveFolderReference> readableFolders)
    {
        IEnumerable<DriveFolderReference> sourceFolders = readableFolders.Where(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials);
        if (UserPermissionPolicy.CanUseOnlyManagementApprovalFolders(userSession.Role))
        {
            sourceFolders = sourceFolders.Where(folder => folder.IsManagementTeam);
        }

        return sourceFolders.OrderBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
    }

    private int getApproverSortOrder(UserAccount user, UserSession userSession)
    {
        return getApproverSortOrder(user.Role, userSession, user.TeamID);
    }

    private int getApproverSortOrder(EUserRole role, UserSession userSession, int? teamIDOrNull = null)
    {
        if (UserRoleUtility.IsManagementMemberRole(userSession.Role))
        {
            switch (role)
            {
                case EUserRole.ManagementDirector:
                    return 0;
                case EUserRole.ManagementDeputyDirector:
                    return 1;
                case EUserRole.ManagementManager:
                    return 2;
                case EUserRole.TeamLeader:
                    return 3;
                case EUserRole.ManagementStaff:
                    return 4;
                case EUserRole.ManagementRecorder:
                    return 5;
                default:
                    return 100;
            }
        }

        if (userSession.TeamID.HasValue && teamIDOrNull.HasValue && teamIDOrNull.Value == userSession.TeamID.Value && role == EUserRole.TeamLeader)
        {
            return 0;
        }

        if (role == EUserRole.TeamLeader)
        {
            return 1;
        }

        if (role == EUserRole.ManagementStaff)
        {
            return 2;
        }

        if (role == EUserRole.ManagementRecorder)
        {
            return 3;
        }

        return 4;
    }

    private List<ApprovalUserOption> createFinalApproverOptions(IReadOnlyList<UserAccount> users)
    {
        List<ApprovalUserOption> options = new List<ApprovalUserOption>();
        foreach (UserAccount user in users)
        {
            if (UserRoleUtility.IsFinalApprovalManagerRole(user.Role) == false)
            {
                continue;
            }

            options.Add(createApprovalUserOption(user));
        }

        return options;
    }

    private List<ApprovalUserOption> createFinalApproverOptions(IReadOnlyList<UserAccount> users, UserSession userSession)
    {
        List<UserAccount> finalApproverUsers = new List<UserAccount>();
        foreach (UserAccount user in users)
        {
            if (user.ID == userSession.UserID)
            {
                continue;
            }

            bool isAllowedFinalApprover = UserPermissionPolicy.CanUseAsFinalApprovalApprover(userSession.Role, user.Role);
            if (isAllowedFinalApprover == false)
            {
                continue;
            }

            finalApproverUsers.Add(user);
        }

        List<ApprovalUserOption> options = new List<ApprovalUserOption>();
        List<UserAccount> orderedFinalApproverUsers = finalApproverUsers.OrderBy(user => getApproverSortOrder(user, userSession)).ThenBy(user => user.DisplayName).ToList();
        foreach (UserAccount user in orderedFinalApproverUsers)
        {
            options.Add(createApprovalUserOption(user));
        }

        return options;
    }

    private ApprovalUserOption createApprovalUserOption(UserAccount user)
    {
        ApprovalUserOption option = new ApprovalUserOption();
        option.UserID = user.ID;
        option.DisplayName = user.DisplayName;
        if (user.Position != null)
        {
            option.RoleName = user.Position.Name;
        }
        else
        {
            option.RoleName = UserRoleUtility.GetDisplayName(user.Role);
        }

        option.Role = user.Role;
        option.TeamID = user.TeamID;
        if (user.Team == null)
        {
            option.TeamName = string.Empty;
        }
        else
        {
            option.TeamName = user.Team.DisplayName;
        }

        return option;
    }

    private IReadOnlyList<int> createAvailableStepCounts(UserSession userSession, IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions)
    {
        List<int> stepCounts = new List<int>();
        if (finalApproverOptions.Count == 0)
        {
            return stepCounts;
        }

        if (UserPermissionPolicy.CanCreateApprovalRequests(userSession.Role) == false)
        {
            return stepCounts;
        }

        if (userSession.Role == EUserRole.TeamLeader || userSession.IsManagementRole())
        {
            if (canBuildApprovalLine(approverOptions, finalApproverOptions, 1))
            {
                stepCounts.Add(1);
            }

            if (canBuildApprovalLine(approverOptions, finalApproverOptions, 2))
            {
                stepCounts.Add(2);
            }

            if (canBuildApprovalLine(approverOptions, finalApproverOptions, 3))
            {
                stepCounts.Add(3);
            }

            return stepCounts;
        }

        if (canBuildApprovalLine(approverOptions, finalApproverOptions, 2))
        {
            stepCounts.Add(2);
        }

        if (canBuildApprovalLine(approverOptions, finalApproverOptions, 3))
        {
            stepCounts.Add(3);
        }

        return stepCounts;
    }

    private bool canBuildApprovalLine(IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions, int stepCount)
    {
        int requiredIntermediateApproverCount = stepCount - 1;
        foreach (ApprovalUserOption finalApproverOption in finalApproverOptions)
        {
            int availableIntermediateApproverCount = approverOptions.Where(option => option.UserID != finalApproverOption.UserID).Select(option => option.UserID).Distinct().Count();
            if (availableIntermediateApproverCount >= requiredIntermediateApproverCount)
            {
                return true;
            }
        }

        return false;
    }

    private ServiceResult validateSubmitForm(ApprovalRequestFormModel formModel, ApprovalCreateContext context)
    {
        if (context.AvailableStepCounts.Contains(formModel.StepCount) == false)
        {
            return ServiceResult.Fail("결재 단계를 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.DestinationFolderID))
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.FileID))
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        if (formModel.StepCount >= 2 && formModel.FirstApproverUserID <= 0)
        {
            return ServiceResult.Fail("1차 결재권자를 선택해 주세요.");
        }

        if (formModel.StepCount >= 2 && context.ApproverOptions.Any(option => option.UserID == formModel.FirstApproverUserID) == false)
        {
            return ServiceResult.Fail("1차 결재권자를 찾을 수 없습니다.");
        }

        if (formModel.StepCount == 3 && formModel.SecondApproverUserID <= 0)
        {
            return ServiceResult.Fail("2차 결재권자를 선택해 주세요.");
        }

        if (formModel.StepCount == 3 && context.ApproverOptions.Any(option => option.UserID == formModel.SecondApproverUserID) == false)
        {
            return ServiceResult.Fail("2차 결재권자를 찾을 수 없습니다.");
        }

        int finalApproverUserID = resolveFinalApproverUserID(context.FinalApproverOptions, formModel.FinalApproverUserID);
        if (finalApproverUserID <= 0)
        {
            return ServiceResult.Fail("최종 결재권자를 선택해 주세요.");
        }

        if (context.FinalApproverOptions.Any(option => option.UserID == finalApproverUserID) == false)
        {
            return ServiceResult.Fail("최종 결재권자를 찾을 수 없습니다.");
        }

        HashSet<int> approverUserIDs = new HashSet<int>();
        if (formModel.StepCount >= 2 && approverUserIDs.Add(formModel.FirstApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (formModel.StepCount == 3 && approverUserIDs.Add(formModel.SecondApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (approverUserIDs.Add(finalApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        return ServiceResult.Succeed();
    }

    private int resolveFinalApproverUserID(IReadOnlyList<ApprovalUserOption> finalApproverOptions, int requestedFinalApproverUserID)
    {
        if (finalApproverOptions.Count == 1)
        {
            return finalApproverOptions[0].UserID;
        }

        return requestedFinalApproverUserID;
    }

    private ApprovalUserOption getRequiredUserOption(IReadOnlyList<ApprovalUserOption> options, int userID)
    {
        ApprovalUserOption? optionOrNull = options.FirstOrDefault(option => option.UserID == userID);
        if (optionOrNull == null)
        {
            throw new InvalidOperationException("Approval user option was not found.");
        }

        return optionOrNull;
    }

    private ApprovalUserOption? getFirstApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestFormModel formModel)
    {
        if (formModel.StepCount < 2)
        {
            return null;
        }

        return getRequiredUserOption(options, formModel.FirstApproverUserID);
    }

    private ApprovalUserOption? getSecondApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestFormModel formModel)
    {
        if (formModel.StepCount != 3)
        {
            return null;
        }

        return getRequiredUserOption(options, formModel.SecondApproverUserID);
    }

    private async Task<bool> isFileInsideFolderAsync(DriveFileMetadata fileMetadata, string sourceFolderID, CancellationToken cancellationToken)
    {
        if (fileMetadata.ParentFolderIDs.Contains(sourceFolderID))
        {
            return true;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(fileMetadata.ID, cancellationToken);
        return ancestorFolderIDs.Contains(sourceFolderID);
    }

    private ApprovalRequest createApprovalRequest(UserSession userSession, ApprovalRequestFormModel formModel, DriveFolderReference sourceFolder, DriveFileMetadata fileMetadata, DriveFileMetadata destinationFolderMetadata)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = getRequestTitle(formModel.Title, fileMetadata.Name);
        request.FileID = fileMetadata.ID;
        request.FileName = fileMetadata.Name;
        request.MimeType = fileMetadata.MimeType;
        request.SourceFolderID = sourceFolder.FolderID;
        request.SourceFolderKind = sourceFolder.FolderKind;
        request.TeamID = sourceFolder.TeamID;
        request.TeamName = getSourceFolderOwnerName(sourceFolder);
        request.RequesterUserID = userSession.UserID;
        request.RequesterDisplayName = userSession.DisplayName;
        request.RequestedAt = DateTime.UtcNow;
        request.Status = EApprovalRequestStatus.Pending;
        request.StepCount = formModel.StepCount;
        request.CurrentStepNumber = 1;
        request.ApprovedFileID = string.Empty;
        request.ApprovedFolderID = formModel.DestinationFolderID.Trim();
        request.ApprovedFolderDisplayName = destinationFolderMetadata.Name;
        return request;
    }

    private ApprovalStep createApprovalStep(int stepNumber, ApprovalUserOption approver)
    {
        ApprovalStep step = new ApprovalStep();
        step.StepNumber = stepNumber;
        step.ApproverUserID = approver.UserID;
        step.ApproverDisplayName = approver.DisplayName;
        step.Status = EApprovalStepStatus.Pending;
        step.Comment = string.Empty;
        return step;
    }

    private string getRequestTitle(string title, string fileName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fileName;
        }

        return title.Trim();
    }

    private string getSourceFolderOwnerName(DriveFolderReference sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder.TeamName) == false)
        {
            return sourceFolder.TeamName;
        }

        return string.Empty;
    }

    private async Task<ApprovalRequest?> loadApprovalRequestOrNullAsync(OrganizationHubDbContext dbContext, int approvalRequestID, CancellationToken cancellationToken)
    {
        ApprovalRequest? requestOrNull = await dbContext.ApprovalRequests.Include(request => request.Team).Include(request => request.RequesterUser).Include(request => request.Steps).Include(request => request.AuditLogs).AsSplitQuery().FirstOrDefaultAsync(request => request.ID == approvalRequestID, cancellationToken);
        return requestOrNull;
    }

    private ApprovalStep? getCurrentStepOrNull(ApprovalRequest request, UserSession userSession)
    {
        if (request.Status != EApprovalRequestStatus.Pending)
        {
            return null;
        }

        return request.Steps.FirstOrDefault(step => step.StepNumber == request.CurrentStepNumber && step.ApproverUserID == userSession.UserID && step.Status == EApprovalStepStatus.Pending);
    }

    private async Task<ServiceResult> changeNextApproverIfRequestedAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, UserSession userSession, int nextApproverUserID, CancellationToken cancellationToken)
    {
        if (nextApproverUserID <= 0)
        {
            return ServiceResult.Succeed();
        }

        int nextStepNumber = request.CurrentStepNumber + 1;
        if (nextStepNumber >= request.StepCount)
        {
            return ServiceResult.Fail("최종 결재권자는 변경할 수 없습니다.");
        }

        ApprovalStep? nextStepOrNull = request.Steps.FirstOrDefault(step => step.StepNumber == nextStepNumber);
        if (nextStepOrNull == null)
        {
            return ServiceResult.Fail("다음 결재 단계를 찾을 수 없습니다.");
        }

        UserAccount? nextApproverOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.ID == nextApproverUserID && user.IsActive, cancellationToken);
        if (nextApproverOrNull == null || canUseAsChangedIntermediateApprover(request, nextApproverOrNull) == false)
        {
            return ServiceResult.Fail("변경할 결재권자를 찾을 수 없습니다.");
        }

        if (nextApproverOrNull.ID == userSession.UserID)
        {
            return ServiceResult.Fail("다음 결재권자를 자기 자신으로 지정할 수 없습니다.");
        }

        foreach (ApprovalStep step in request.Steps)
        {
            if (step.StepNumber == nextStepNumber)
            {
                continue;
            }

            if (step.ApproverUserID == nextApproverOrNull.ID)
            {
                return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
            }
        }

        string previousApproverDisplayName = nextStepOrNull.ApproverDisplayName;
        nextStepOrNull.ApproverUserID = nextApproverOrNull.ID;
        nextStepOrNull.ApproverDisplayName = nextApproverOrNull.DisplayName;
        request.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.ApproverChanged, nextStepNumber, "결재선 변경", previousApproverDisplayName, nextApproverOrNull.DisplayName));
        return ServiceResult.Succeed();
    }

    private bool canUseAsChangedIntermediateApprover(ApprovalRequest request, UserAccount nextApprover)
    {
        if (nextApprover.ID == request.RequesterUserID)
        {
            return false;
        }

        EUserRole requesterRole = getRequesterRole(request);
        return UserPermissionPolicy.CanUseAsIntermediateApprovalApprover(requesterRole, nextApprover.Role);
    }

    private async Task<ServiceResult> copyApprovedFileIfConfiguredAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderID))
        {
            request.ApprovedFileID = request.FileID;
            request.ApprovedFolderID = string.Empty;
            request.ApprovedFolderDisplayName = string.Empty;
            return ServiceResult.Succeed();
        }

        try
        {
            DriveFileItem copiedFile = await mDriveService.CopyFileToFolderAsync(request.FileID, request.ApprovedFolderID.Trim(), request.FileName, cancellationToken);
            request.ApprovedFileID = copiedFile.ID;
            dbContext.UploadAuditLogs.Add(createUploadAuditLogForApprovedCopy(request, copiedFile));
            if (mDriveBrowserServiceOrNull != null)
            {
                mDriveBrowserServiceOrNull.InvalidateRecentFiles(request.ApprovedFolderID.Trim());
            }

            return ServiceResult.Succeed();
        }
        catch (Exception exception)
        {
            return ServiceResult.Fail("결재 완료 자료 폴더로 파일을 복사하지 못했습니다. " + exception.Message);
        }
    }

    private UploadAuditLog createUploadAuditLogForApprovedCopy(ApprovalRequest request, DriveFileItem copiedFile)
    {
        UploadAuditLog auditLog = new UploadAuditLog();
        auditLog.FileID = copiedFile.ID;
        auditLog.FileName = copiedFile.Name;
        auditLog.FolderID = request.ApprovedFolderID.Trim();
        auditLog.FolderDisplayName = request.ApprovedFolderDisplayName;
        auditLog.FolderKind = EDriveFolderKind.TeamApproved;
        auditLog.UploaderUserID = request.RequesterUserID;
        auditLog.UploaderDisplayName = request.RequesterDisplayName;
        auditLog.UploaderRole = getRequesterRole(request);
        auditLog.TeamID = request.TeamID;
        auditLog.TeamName = request.TeamName;
        auditLog.UploadedAt = DateTime.UtcNow;
        return auditLog;
    }

    private EUserRole getRequesterRole(ApprovalRequest request)
    {
        if (request.RequesterUser != null)
        {
            return request.RequesterUser.Role;
        }

        return EUserRole.TeamUser;
    }

    private void bumpApprovalVersion()
    {
        if (mChangeVersionServiceOrNull == null)
        {
            return;
        }

        mChangeVersionServiceOrNull.Bump(ChangeVersionKeys.APPROVAL);
    }

    private async Task<List<ApprovalRequest>> filterApprovedRequestsByDestinationAsync(UserSession userSession, IReadOnlyList<ApprovalRequest> requests, CancellationToken cancellationToken)
    {
        List<ApprovalRequest> readableRequests = new List<ApprovalRequest>();
        foreach (ApprovalRequest request in requests)
        {
            bool canReadDestination = await canReadApprovedRequestDestinationAsync(userSession, request, cancellationToken);
            if (canReadDestination)
            {
                readableRequests.Add(request);
            }
        }

        return readableRequests;
    }

    private async Task<bool> canReadApprovedRequestDestinationAsync(UserSession userSession, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderID))
        {
            return false;
        }

        return await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, request.ApprovedFolderID, cancellationToken);
    }

    private ApprovalAuditLog createAuditLog(UserSession userSession, EApprovalAuditAction action, int? stepNumber, string comment, string previousApproverDisplayName, string newApproverDisplayName)
    {
        ApprovalAuditLog auditLog = new ApprovalAuditLog();
        auditLog.ActorUserID = userSession.UserID;
        auditLog.ActorDisplayName = userSession.DisplayName;
        auditLog.Action = action;
        auditLog.StepNumber = stepNumber;
        auditLog.Comment = getTrimmedComment(comment);
        auditLog.PreviousApproverDisplayName = previousApproverDisplayName;
        auditLog.NewApproverDisplayName = newApproverDisplayName;
        auditLog.CreatedAt = DateTime.UtcNow;
        return auditLog;
    }

    private string getTrimmedComment(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        return comment.Trim();
    }

    private IReadOnlyList<ApprovalRequestItem> createApprovalRequestItems(IReadOnlyList<ApprovalRequest> requests)
    {
        List<ApprovalRequestItem> items = new List<ApprovalRequestItem>();
        foreach (ApprovalRequest request in requests)
        {
            items.Add(createApprovalRequestItem(request));
        }

        return items;
    }

    private ApprovalRequestItem createApprovalRequestItem(ApprovalRequest request)
    {
        ApprovalRequestItem item = new ApprovalRequestItem();
        item.ID = request.ID;
        item.Title = request.Title;
        item.FileID = request.FileID;
        item.FileName = request.FileName;
        item.ApprovedFileID = request.ApprovedFileID;
        item.ApprovedFolderDisplayName = request.ApprovedFolderDisplayName;
        item.TeamName = request.TeamName;
        item.RequesterDisplayName = request.RequesterDisplayName;
        item.RequestedAt = request.RequestedAt;
        item.Status = request.Status;
        item.StepCount = request.StepCount;
        item.CurrentStepNumber = request.CurrentStepNumber;
        item.CompletedAt = request.CompletedAt;
        item.RejectedAt = request.RejectedAt;
        item.Steps = request.Steps.OrderBy(step => step.StepNumber).Select(createApprovalStepItem).ToList();
        item.AuditLogs = request.AuditLogs.OrderByDescending(auditLog => auditLog.CreatedAt).Select(createApprovalAuditLogItem).ToList();
        return item;
    }

    private ApprovalStepItem createApprovalStepItem(ApprovalStep step)
    {
        ApprovalStepItem item = new ApprovalStepItem();
        item.StepNumber = step.StepNumber;
        item.ApproverUserID = step.ApproverUserID;
        item.ApproverDisplayName = step.ApproverDisplayName;
        item.Status = step.Status;
        item.ActionedAt = step.ActionedAt;
        item.Comment = step.Comment;
        return item;
    }

    private ApprovalAuditLogItem createApprovalAuditLogItem(ApprovalAuditLog auditLog)
    {
        ApprovalAuditLogItem item = new ApprovalAuditLogItem();
        item.Action = auditLog.Action;
        item.ActorDisplayName = auditLog.ActorDisplayName;
        item.StepNumber = auditLog.StepNumber;
        item.Comment = auditLog.Comment;
        item.PreviousApproverDisplayName = auditLog.PreviousApproverDisplayName;
        item.NewApproverDisplayName = auditLog.NewApproverDisplayName;
        item.CreatedAt = auditLog.CreatedAt;
        return item;
    }

    private sealed class ConfiguredApprovalLine
    {
        public int StepCount
        {
            get; private init;
        }
        public IReadOnlyList<ApprovalUserOption> IntermediateApprovers
        {
            get; private init;
        }
        public IReadOnlyList<ApprovalUserOption> FinalApprovers
        {
            get; private init;
        }
        public ApprovalDefaultLineOption DefaultLine
        {
            get; private init;
        }

        public ConfiguredApprovalLine(int stepCount, IReadOnlyList<ApprovalUserOption> intermediateApprovers, IReadOnlyList<ApprovalUserOption> finalApprovers, ApprovalDefaultLineOption defaultLine)
        {
            StepCount = stepCount;
            IntermediateApprovers = intermediateApprovers;
            FinalApprovers = finalApprovers;
            DefaultLine = defaultLine;
        }
    }
}
