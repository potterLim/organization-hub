using System;
using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public static partial class ApprovalDefaultLinePolicy
{
    private static ApprovalUserId getPreferredFinalApproverId(
        UserSession userSession,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        HashSet<ApprovalUserId> excludedUserIds,
        ApprovalStepCount stepCount)
    {
        return getPreferredOptionId(
            finalApproverOptions,
            getFinalApproverPreferences(userSession.Role),
            createUserTeamIdOrNull(userSession),
            excludedUserIds,
            option => canCompleteLineWithFinalApprover(approverOptions, option, stepCount));
    }

    private static ApprovalUserId getPreferredIntermediateApproverId(
        UserSession userSession,
        ApprovalStepCount stepCount,
        EApprovalIntermediateApproverSlot approverSlot,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        HashSet<ApprovalUserId> excludedUserIds)
    {
        return getPreferredOptionId(
            approverOptions,
            getIntermediateApproverPreferences(userSession.Role, stepCount, approverSlot),
            createUserTeamIdOrNull(userSession),
            excludedUserIds);
    }

    private static ApprovalUserId getPreferredOptionId(
        IReadOnlyList<ApprovalUserOption> options,
        IReadOnlyList<ApprovalApproverPreference> preferences,
        OrganizationTeamId? requesterTeamIdOrNull,
        HashSet<ApprovalUserId> excludedUserIds)
    {
        return getPreferredOptionId(options, preferences, requesterTeamIdOrNull, excludedUserIds, null);
    }

    private static ApprovalUserId getPreferredOptionId(
        IReadOnlyList<ApprovalUserOption> options,
        IReadOnlyList<ApprovalApproverPreference> preferences,
        OrganizationTeamId? requesterTeamIdOrNull,
        HashSet<ApprovalUserId> excludedUserIds,
        Func<ApprovalUserOption, bool>? isAllowedOptionOrNull)
    {
        foreach (ApprovalApproverPreference preference in preferences)
        {
            ApprovalUserOption? preferredOptionOrNull = options.FirstOrDefault(option =>
                isPreferredOption(option, preference, requesterTeamIdOrNull, excludedUserIds) &&
                isAllowedOption(isAllowedOptionOrNull, option));
            if (preferredOptionOrNull != null)
            {
                return ApprovalUserId.CreateExisting(preferredOptionOrNull.UserId);
            }
        }

        ApprovalUserOption? fallbackOptionOrNull = options.FirstOrDefault(option =>
            excludedUserIds.Contains(ApprovalUserId.CreateExisting(option.UserId)) == false &&
            isAllowedOption(isAllowedOptionOrNull, option));
        if (fallbackOptionOrNull == null)
        {
            return ApprovalUserId.Empty;
        }

        return ApprovalUserId.CreateExisting(fallbackOptionOrNull.UserId);
    }

    private static bool isAllowedOption(Func<ApprovalUserOption, bool>? isAllowedOptionOrNull, ApprovalUserOption option)
    {
        return isAllowedOptionOrNull == null || isAllowedOptionOrNull(option);
    }

    private static bool canCompleteLineWithFinalApprover(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        ApprovalUserOption finalApproverOption,
        ApprovalStepCount stepCount)
    {
        int requiredIntermediateApproverCount = stepCount.GetIntermediateApproverCount();
        int availableIntermediateApproverCount = approverOptions
            .Where(option => option.UserId != finalApproverOption.UserId)
            .Select(option => ApprovalUserId.CreateExisting(option.UserId))
            .Distinct()
            .Count();
        return availableIntermediateApproverCount >= requiredIntermediateApproverCount;
    }

    private static bool isPreferredOption(
        ApprovalUserOption option,
        ApprovalApproverPreference preference,
        OrganizationTeamId? requesterTeamIdOrNull,
        HashSet<ApprovalUserId> excludedUserIds)
    {
        ApprovalUserId optionUserId = ApprovalUserId.CreateExisting(option.UserId);
        if (excludedUserIds.Contains(optionUserId) || option.Role != preference.Role)
        {
            return false;
        }

        if (preference.RequiresRequesterTeam)
        {
            int? requesterTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(requesterTeamIdOrNull);
            return requesterTeamIdValueOrNull.HasValue && option.TeamId == requesterTeamIdValueOrNull;
        }

        return true;
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
