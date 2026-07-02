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
        UserSession userSession)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        List<ApprovalUserOption> options = new List<ApprovalUserOption>();
        List<UserAccount> approverUsers = users
            .Where(user => user.Id != currentUserId)
            .Where(user => canUseAsIntermediateApprover(user, userSession))
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
        UserSession userSession)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        List<UserAccount> finalApproverUsers = new List<UserAccount>();
        foreach (UserAccount user in users)
        {
            if (user.Id == currentUserId)
            {
                continue;
            }

            bool isAllowedFinalApprover = canUseAsFinalApprover(user, userSession);
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
        UserSession userSession)
    {
        return hasUserApprovalPermission(user, userSession, new PermissionKey(PermissionKeys.APPROVAL_APPROVE));
    }

    private bool canUseAsFinalApprover(
        UserAccount user,
        UserSession userSession)
    {
        return hasUserApprovalPermission(user, userSession, new PermissionKey(PermissionKeys.APPROVAL_FINAL_APPROVE));
    }

    private ApprovalUserOption createApprovalUserOption(UserAccount user)
    {
        ApprovalUserOption option = new ApprovalUserOption();
        option.UserId = user.Id;
        option.DisplayName = user.DisplayName;
        option.PositionId = user.PositionId;
        if (user.Position != null)
        {
            option.PositionName = user.Position.Name;
        }
        else
        {
            option.PositionName = "직책 미지정";
        }

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
