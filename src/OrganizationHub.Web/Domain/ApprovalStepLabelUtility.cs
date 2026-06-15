using System.Diagnostics;
using System.Globalization;

namespace OrganizationHub.Web.Domain;

public static class ApprovalStepLabelUtility
{
    public static string GetStepRoleName(ApprovalStepCount stepCount, int stepNumber)
    {
        if (stepNumber < ApprovalStepCount.MINIMUM_VALUE || stepNumber > stepCount.Value)
        {
            Debug.Fail("invalid approval step number");
            return "-";
        }

        if (stepNumber == stepCount.Value)
        {
            return "결재권자";
        }

        int reviewStepCount = stepCount.Value - 1;
        if (reviewStepCount == 1)
        {
            return "검토자";
        }

        return stepNumber.ToString(CultureInfo.InvariantCulture) + "차 검토자";
    }

    public static string GetStepOptionLabel(ApprovalStepCount stepCount)
    {
        if (stepCount.Value == 1)
        {
            return "결재권자";
        }

        if (stepCount.Value == 2)
        {
            return "검토자 + 결재권자";
        }

        return "1차 검토자 + 2차 검토자 + 결재권자";
    }

    public static string GetStepDescription(ApprovalStepCount stepCount)
    {
        if (stepCount.Value == 1)
        {
            return "결재권자에게 바로 요청합니다.";
        }

        if (stepCount.Value == 2)
        {
            return "검토 후 결재권자에게 요청합니다.";
        }

        return "1차, 2차 검토 후 결재권자에게 요청합니다.";
    }
}
