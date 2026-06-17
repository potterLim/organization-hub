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

public sealed partial class ApprovalService
{
    public async Task<ApprovalCreateContext> GetCreateContextAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        ApprovalCreateContext context = new ApprovalCreateContext();
        PermissionDecision createApprovalRequestDecision = UserPermissionPolicy.DecideCreateApprovalRequest(userSession.Role);
        if (createApprovalRequestDecision.IsAllowed == false)
        {
            context.BlockReason = createApprovalRequestDecision.Reason;
            return context;
        }

        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        OrganizationTeamId? userTeamIdOrNull = createUserTeamIdOrNull(userSession);
        bool canCreateApprovalRequest;
        if (usesConfiguredPermissions)
        {
            canCreateApprovalRequest = await mOrganizationPermissionService.HasPermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.APPROVAL_REQUEST),
                userTeamIdOrNull,
                cancellationToken);
        }
        else
        {
            canCreateApprovalRequest = UserPermissionPolicy.CanCreateApprovalRequests(userSession.Role);
        }

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
            List<UserAccount> users = await loadActiveApprovalUsersAsync(dbContext, cancellationToken);
            ConfiguredApprovalLine? configuredLineOrNull = await createConfiguredApprovalLineOrNullAsync(dbContext, users, userSession, cancellationToken);
            if (configuredLineOrNull != null)
            {
                applyConfiguredApprovalLine(context, sourceFolders, configuredLineOrNull);
                return context;
            }

            context.ApproverOptions = createApproverOptions(users, userSession, usesConfiguredPermissions);
            context.FinalApproverCandidates = createFinalApproverOptions(users, userSession, usesConfiguredPermissions);
            context.FinalApproverOptions = createFinalApproverOptions(users, userSession, usesConfiguredPermissions);
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

    private void applyConfiguredApprovalLine(
        ApprovalCreateContext context,
        IReadOnlyList<DriveFolderReference> sourceFolders,
        ConfiguredApprovalLine configuredLine)
    {
        context.ApproverOptions = configuredLine.IntermediateApprovers;
        context.FinalApproverOptions = configuredLine.FinalApprovers;
        context.FinalApproverCandidates = configuredLine.FinalApprovers;
        context.AvailableStepCounts = new List<ApprovalStepCount> { configuredLine.StepCount };
        context.DefaultLineOptions = new List<ApprovalDefaultLineOption> { configuredLine.DefaultLine };
        context.CanCreateRequest = sourceFolders.Count > 0 && context.DestinationFolders.Count > 0 && context.FinalApproverOptions.Count > 0;
        if (context.CanCreateRequest == false)
        {
            context.BlockReason = "결재 요청을 만들 수 없습니다.";
        }
    }

    private static async Task<List<UserAccount>> loadActiveApprovalUsersAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        List<UserAccount> users = await dbContext.UserAccounts
            .Include(user => user.Team)
            .Include(user => user.Position)
            .Where(user => user.IsActive)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);

        List<int> positionIds = users
            .Where(user => user.PositionId.HasValue)
            .Select(user => user.PositionId.GetValueOrDefault())
            .Distinct()
            .ToList();
        if (positionIds.Count > 0)
        {
            await dbContext.PositionPermissionGrants
                .Where(permissionGrant => positionIds.Contains(permissionGrant.PositionId))
                .LoadAsync(cancellationToken);
        }

        return users;
    }

    private static OrganizationTeamId? createUserTeamIdOrNull(UserSession userSession)
    {
        if (userSession.HasTeam == false)
        {
            return null;
        }

        return userSession.GetRequiredOrganizationTeamId();
    }
}
