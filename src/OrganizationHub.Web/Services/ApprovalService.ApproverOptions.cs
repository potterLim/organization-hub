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
}
