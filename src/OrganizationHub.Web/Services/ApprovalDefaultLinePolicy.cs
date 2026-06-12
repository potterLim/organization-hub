using System;
using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public static class ApprovalDefaultLinePolicy
{
    private static readonly IReadOnlyList<ApprovalApproverPreference> DIRECTOR_FINAL_PREFERENCES = createPreferences(
        EUserRole.ManagementDeputyDirector,
        EUserRole.ManagementManager,
        EUserRole.TeamLeader,
        EUserRole.ManagementStaff,
        EUserRole.ManagementRecorder);

    private static readonly IReadOnlyList<ApprovalApproverPreference> MANAGEMENT_FINAL_PREFERENCES = createPreferences(
        EUserRole.ManagementDirector,
        EUserRole.ManagementDeputyDirector,
        EUserRole.ManagementManager,
        EUserRole.TeamLeader,
        EUserRole.ManagementStaff,
        EUserRole.ManagementRecorder);

    private static readonly IReadOnlyList<ApprovalApproverPreference> TEAM_FINAL_PREFERENCES = createPreferences(EUserRole.ManagementManager);
    private static readonly IReadOnlyList<ApprovalApproverPreference> TREASURER_ACCOUNTANT_PREFERENCES = createPreferences(EUserRole.ManagementManager, EUserRole.ManagementStaff);
    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_TREASURER_PREFERENCES = createPreferences(EUserRole.ManagementStaff, EUserRole.ManagementManager);
    private static readonly IReadOnlyList<ApprovalApproverPreference> DEPUTY_ACCOUNTANT_PREFERENCES = createPreferences(EUserRole.ManagementDeputyDirector, EUserRole.ManagementStaff);
    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_DEPUTY_PREFERENCES = createPreferences(EUserRole.ManagementStaff, EUserRole.ManagementDeputyDirector);
    private static readonly IReadOnlyList<ApprovalApproverPreference> TREASURER_SECRETARY_PREFERENCES = createPreferences(EUserRole.ManagementManager, EUserRole.ManagementRecorder);
    private static readonly IReadOnlyList<ApprovalApproverPreference> SECRETARY_TREASURER_PREFERENCES = createPreferences(EUserRole.ManagementRecorder, EUserRole.ManagementManager);
    private static readonly IReadOnlyList<ApprovalApproverPreference> SECRETARY_ACCOUNTANT_PREFERENCES = createPreferences(EUserRole.ManagementRecorder, EUserRole.ManagementStaff);
    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_SECRETARY_PREFERENCES = createPreferences(EUserRole.ManagementStaff, EUserRole.ManagementRecorder);
    private static readonly IReadOnlyList<ApprovalApproverPreference> OWN_TEAM_LEADER_ACCOUNTANT_PREFERENCES = new List<ApprovalApproverPreference>
    {
        new ApprovalApproverPreference(EUserRole.TeamLeader, EApprovalRequesterTeamRequirement.Required),
        new ApprovalApproverPreference(EUserRole.ManagementStaff)
    };

    public static IReadOnlyList<ApprovalDefaultLineOption> CreateDefaultLineOptions(UserSession userSession, IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions, IReadOnlyList<int> availableStepCounts)
    {
        List<ApprovalDefaultLineOption> defaultLineOptions = new List<ApprovalDefaultLineOption>();
        foreach (int stepCount in availableStepCounts)
        {
            HashSet<int> selectedUserIDs = new HashSet<int>();
            int finalApproverUserID = getPreferredFinalApproverID(userSession, approverOptions, finalApproverOptions, selectedUserIDs, stepCount);
            addSelectedUserID(selectedUserIDs, finalApproverUserID);

            int firstApproverUserID = 0;
            if (stepCount >= 2)
            {
                firstApproverUserID = getPreferredIntermediateApproverID(userSession, stepCount, 1, approverOptions, selectedUserIDs);
                addSelectedUserID(selectedUserIDs, firstApproverUserID);
            }

            int secondApproverUserID = 0;
            if (stepCount == 3)
            {
                secondApproverUserID = getPreferredIntermediateApproverID(userSession, stepCount, 2, approverOptions, selectedUserIDs);
                addSelectedUserID(selectedUserIDs, secondApproverUserID);
            }

            ApprovalDefaultLineOption defaultLineOption = new ApprovalDefaultLineOption();
            defaultLineOption.StepCount = stepCount;
            defaultLineOption.FirstApproverUserID = firstApproverUserID;
            defaultLineOption.SecondApproverUserID = secondApproverUserID;
            defaultLineOption.FinalApproverUserID = finalApproverUserID;
            defaultLineOptions.Add(defaultLineOption);
        }

        return defaultLineOptions;
    }

    private static int getPreferredFinalApproverID(UserSession userSession, IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions, HashSet<int> excludedUserIDs, int stepCount)
    {
        return getPreferredOptionID(finalApproverOptions, getFinalApproverPreferences(userSession.Role), userSession.TeamID, excludedUserIDs, option => canCompleteLineWithFinalApprover(approverOptions, option, stepCount));
    }

    private static int getPreferredIntermediateApproverID(UserSession userSession, int stepCount, int slotNumber, IReadOnlyList<ApprovalUserOption> approverOptions, HashSet<int> excludedUserIDs)
    {
        return getPreferredOptionID(approverOptions, getIntermediateApproverPreferences(userSession.Role, stepCount, slotNumber), userSession.TeamID, excludedUserIDs);
    }

    private static IReadOnlyList<ApprovalApproverPreference> getFinalApproverPreferences(EUserRole requesterRole)
    {
        switch (requesterRole)
        {
            case EUserRole.ManagementDirector:
                return DIRECTOR_FINAL_PREFERENCES;
            case EUserRole.ManagementDeputyDirector:
            case EUserRole.ManagementManager:
            case EUserRole.ManagementStaff:
            case EUserRole.ManagementRecorder:
                return MANAGEMENT_FINAL_PREFERENCES;
            default:
                return TEAM_FINAL_PREFERENCES;
        }
    }

    private static IReadOnlyList<ApprovalApproverPreference> getIntermediateApproverPreferences(EUserRole requesterRole, int stepCount, int slotNumber)
    {
        switch (requesterRole)
        {
            case EUserRole.ManagementDirector:
            case EUserRole.ManagementDeputyDirector:
                return stepCount == 3 && slotNumber == 1 ? ACCOUNTANT_TREASURER_PREFERENCES : TREASURER_ACCOUNTANT_PREFERENCES;
            case EUserRole.ManagementManager:
                return stepCount == 3 && slotNumber == 1 ? ACCOUNTANT_DEPUTY_PREFERENCES : DEPUTY_ACCOUNTANT_PREFERENCES;
            case EUserRole.ManagementStaff:
                return stepCount == 3 && slotNumber == 1 ? SECRETARY_TREASURER_PREFERENCES : TREASURER_SECRETARY_PREFERENCES;
            case EUserRole.ManagementRecorder:
                return stepCount == 3 && slotNumber == 1 ? ACCOUNTANT_TREASURER_PREFERENCES : TREASURER_ACCOUNTANT_PREFERENCES;
            case EUserRole.TeamLeader:
                return stepCount == 3 && slotNumber == 1 ? SECRETARY_ACCOUNTANT_PREFERENCES : ACCOUNTANT_SECRETARY_PREFERENCES;
            default:
                return slotNumber == 1 ? OWN_TEAM_LEADER_ACCOUNTANT_PREFERENCES : ACCOUNTANT_SECRETARY_PREFERENCES;
        }
    }

    private static List<ApprovalApproverPreference> createPreferences(params EUserRole[] roles)
    {
        List<ApprovalApproverPreference> preferences = new List<ApprovalApproverPreference>();
        foreach (EUserRole role in roles)
        {
            preferences.Add(new ApprovalApproverPreference(role));
        }

        return preferences;
    }

    private static int getPreferredOptionID(IReadOnlyList<ApprovalUserOption> options, IReadOnlyList<ApprovalApproverPreference> preferences, int? requesterTeamIDOrNull, HashSet<int> excludedUserIDs)
    {
        return getPreferredOptionID(options, preferences, requesterTeamIDOrNull, excludedUserIDs, null);
    }

    private static int getPreferredOptionID(IReadOnlyList<ApprovalUserOption> options, IReadOnlyList<ApprovalApproverPreference> preferences, int? requesterTeamIDOrNull, HashSet<int> excludedUserIDs, Func<ApprovalUserOption, bool>? isAllowedOptionOrNull)
    {
        foreach (ApprovalApproverPreference preference in preferences)
        {
            ApprovalUserOption? preferredOptionOrNull = options.FirstOrDefault(option => isPreferredOption(option, preference, requesterTeamIDOrNull, excludedUserIDs) && isAllowedOption(isAllowedOptionOrNull, option));
            if (preferredOptionOrNull != null)
            {
                return preferredOptionOrNull.UserID;
            }
        }

        ApprovalUserOption? fallbackOptionOrNull = options.FirstOrDefault(option => excludedUserIDs.Contains(option.UserID) == false && isAllowedOption(isAllowedOptionOrNull, option));
        if (fallbackOptionOrNull == null)
        {
            return 0;
        }

        return fallbackOptionOrNull.UserID;
    }

    private static bool isAllowedOption(Func<ApprovalUserOption, bool>? isAllowedOptionOrNull, ApprovalUserOption option)
    {
        return isAllowedOptionOrNull == null || isAllowedOptionOrNull(option);
    }

    private static bool canCompleteLineWithFinalApprover(IReadOnlyList<ApprovalUserOption> approverOptions, ApprovalUserOption finalApproverOption, int stepCount)
    {
        int requiredIntermediateApproverCount = stepCount - 1;
        int availableIntermediateApproverCount = approverOptions.Where(option => option.UserID != finalApproverOption.UserID).Select(option => option.UserID).Distinct().Count();
        return availableIntermediateApproverCount >= requiredIntermediateApproverCount;
    }

    private static bool isPreferredOption(ApprovalUserOption option, ApprovalApproverPreference preference, int? requesterTeamIDOrNull, HashSet<int> excludedUserIDs)
    {
        if (excludedUserIDs.Contains(option.UserID) || option.Role != preference.Role)
        {
            return false;
        }

        if (preference.RequiresRequesterTeam)
        {
            return requesterTeamIDOrNull.HasValue && option.TeamID == requesterTeamIDOrNull.Value;
        }

        return true;
    }

    private static void addSelectedUserID(HashSet<int> selectedUserIDs, int userID)
    {
        if (userID > 0)
        {
            selectedUserIDs.Add(userID);
        }
    }

    private sealed class ApprovalApproverPreference
    {
        public EUserRole Role
        {
            get; private init;
        }
        public bool RequiresRequesterTeam
        {
            get; private init;
        }

        public ApprovalApproverPreference(EUserRole role, EApprovalRequesterTeamRequirement requesterTeamRequirement = EApprovalRequesterTeamRequirement.Optional)
        {
            Role = role;
            RequiresRequesterTeam = requesterTeamRequirement == EApprovalRequesterTeamRequirement.Required;
        }
    }
}
