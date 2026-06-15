using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private List<ApprovalUserOption> createApproverOptions(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        bool usesConfiguredPermissions)
    {
        List<ApprovalUserOption> options = new List<ApprovalUserOption>();
        List<UserAccount> approverUsers = users
            .Where(user => user.Id != userSession.UserId)
            .Where(user => canUseAsIntermediateApprover(user, userSession, usesConfiguredPermissions))
            .OrderBy(user => getApproverSortOrder(user, userSession))
            .ThenBy(user => user.DisplayName)
            .ToList();
        foreach (UserAccount user in approverUsers)
        {
            options.Add(createApprovalUserOption(user));
        }

        return options;
    }

    private List<ApprovalUserOption> createFinalApproverOptions(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        bool usesConfiguredPermissions)
    {
        List<UserAccount> finalApproverUsers = new List<UserAccount>();
        foreach (UserAccount user in users)
        {
            if (user.Id == userSession.UserId)
            {
                continue;
            }

            bool isAllowedFinalApprover = canUseAsFinalApprover(user, userSession, usesConfiguredPermissions);
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

    private bool canUseAsIntermediateApprover(
        UserAccount user,
        UserSession userSession,
        bool usesConfiguredPermissions)
    {
        if (usesConfiguredPermissions)
        {
            return userHasApprovalPermission(user, userSession, PermissionKeys.APPROVAL_APPROVE);
        }

        return UserPermissionPolicy.CanUseAsIntermediateApprovalApprover(userSession.Role, user.Role);
    }

    private bool canUseAsFinalApprover(
        UserAccount user,
        UserSession userSession,
        bool usesConfiguredPermissions)
    {
        if (usesConfiguredPermissions)
        {
            return userHasApprovalPermission(user, userSession, PermissionKeys.APPROVAL_FINAL_APPROVE);
        }

        return UserPermissionPolicy.CanUseAsFinalApprovalApprover(userSession.Role, user.Role);
    }

    private ApprovalUserOption createApprovalUserOption(UserAccount user)
    {
        ApprovalUserOption option = new ApprovalUserOption();
        option.UserId = user.Id;
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
        option.TeamId = user.TeamId;
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
}
