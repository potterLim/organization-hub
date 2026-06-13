using System;
using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
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
}
