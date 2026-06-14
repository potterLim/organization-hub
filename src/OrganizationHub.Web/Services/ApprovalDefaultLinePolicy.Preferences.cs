using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public static partial class ApprovalDefaultLinePolicy
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

    private static readonly IReadOnlyList<ApprovalApproverPreference> TEAM_FINAL_PREFERENCES = createPreferences(
        EUserRole.ManagementManager);

    private static readonly IReadOnlyList<ApprovalApproverPreference> TREASURER_ACCOUNTANT_PREFERENCES = createPreferences(
        EUserRole.ManagementManager,
        EUserRole.ManagementStaff);

    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_TREASURER_PREFERENCES = createPreferences(
        EUserRole.ManagementStaff,
        EUserRole.ManagementManager);

    private static readonly IReadOnlyList<ApprovalApproverPreference> DEPUTY_ACCOUNTANT_PREFERENCES = createPreferences(
        EUserRole.ManagementDeputyDirector,
        EUserRole.ManagementStaff);

    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_DEPUTY_PREFERENCES = createPreferences(
        EUserRole.ManagementStaff,
        EUserRole.ManagementDeputyDirector);

    private static readonly IReadOnlyList<ApprovalApproverPreference> TREASURER_SECRETARY_PREFERENCES = createPreferences(
        EUserRole.ManagementManager,
        EUserRole.ManagementRecorder);

    private static readonly IReadOnlyList<ApprovalApproverPreference> SECRETARY_TREASURER_PREFERENCES = createPreferences(
        EUserRole.ManagementRecorder,
        EUserRole.ManagementManager);

    private static readonly IReadOnlyList<ApprovalApproverPreference> SECRETARY_ACCOUNTANT_PREFERENCES = createPreferences(
        EUserRole.ManagementRecorder,
        EUserRole.ManagementStaff);

    private static readonly IReadOnlyList<ApprovalApproverPreference> ACCOUNTANT_SECRETARY_PREFERENCES = createPreferences(
        EUserRole.ManagementStaff,
        EUserRole.ManagementRecorder);

    private static readonly IReadOnlyList<ApprovalApproverPreference> OWN_TEAM_LEADER_ACCOUNTANT_PREFERENCES = new List<ApprovalApproverPreference>
    {
        new ApprovalApproverPreference(EUserRole.TeamLeader, EApprovalRequesterTeamRequirement.Required),
        new ApprovalApproverPreference(EUserRole.ManagementStaff)
    };

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

    private static IReadOnlyList<ApprovalApproverPreference> getIntermediateApproverPreferences(
        EUserRole requesterRole,
        ApprovalStepCount stepCount,
        EApprovalIntermediateApproverSlot approverSlot)
    {
        bool shouldUseFirstApproverPreference = stepCount.RequiresSecondApprover && approverSlot == EApprovalIntermediateApproverSlot.First;
        switch (requesterRole)
        {
            case EUserRole.ManagementDirector:
            case EUserRole.ManagementDeputyDirector:
                return shouldUseFirstApproverPreference ? ACCOUNTANT_TREASURER_PREFERENCES : TREASURER_ACCOUNTANT_PREFERENCES;
            case EUserRole.ManagementManager:
                return shouldUseFirstApproverPreference ? ACCOUNTANT_DEPUTY_PREFERENCES : DEPUTY_ACCOUNTANT_PREFERENCES;
            case EUserRole.ManagementStaff:
                return shouldUseFirstApproverPreference ? SECRETARY_TREASURER_PREFERENCES : TREASURER_SECRETARY_PREFERENCES;
            case EUserRole.ManagementRecorder:
                return shouldUseFirstApproverPreference ? ACCOUNTANT_TREASURER_PREFERENCES : TREASURER_ACCOUNTANT_PREFERENCES;
            case EUserRole.TeamLeader:
                return shouldUseFirstApproverPreference ? SECRETARY_ACCOUNTANT_PREFERENCES : ACCOUNTANT_SECRETARY_PREFERENCES;
            default:
                return approverSlot == EApprovalIntermediateApproverSlot.First ? OWN_TEAM_LEADER_ACCOUNTANT_PREFERENCES : ACCOUNTANT_SECRETARY_PREFERENCES;
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

        public ApprovalApproverPreference(
            EUserRole role,
            EApprovalRequesterTeamRequirement requesterTeamRequirement = EApprovalRequesterTeamRequirement.Optional)
        {
            Role = role;
            RequiresRequesterTeam = requesterTeamRequirement == EApprovalRequesterTeamRequirement.Required;
        }
    }

    private enum EApprovalIntermediateApproverSlot
    {
        First,
        Second
    }
}
