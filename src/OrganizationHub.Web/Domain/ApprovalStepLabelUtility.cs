using System.Diagnostics;
using System.Globalization;

namespace OrganizationHub.Web.Domain;

public static class ApprovalStepLabelUtility
{
    public static string GetStepLabel(ApprovalStepCount stepCount, ApprovalStepNumber stepNumber)
    {
        if (stepCount.ContainsStepNumber(stepNumber) == false)
        {
            Debug.Fail("invalid approval step number");
            return "-";
        }

        if (stepNumber.IsFinalStepOf(stepCount))
        {
            return "결재권자";
        }

        int reviewStepCount = stepCount.GetIntermediateApproverCount();
        if (reviewStepCount == 1)
        {
            return "검토자";
        }

        return stepNumber.GetRequiredValue().ToString(CultureInfo.InvariantCulture) + "차 검토자";
    }

    public static string GetStepOptionLabel(ApprovalStepCount stepCount)
    {
        if (stepCount.IsSingleStep)
        {
            return "결재권자";
        }

        if (stepCount.RequiresSecondApprover() == false)
        {
            return "검토자 + 결재권자";
        }

        return "1차 검토자 + 2차 검토자 + 결재권자";
    }

    public static string GetStepDescription(ApprovalStepCount stepCount)
    {
        if (stepCount.IsSingleStep)
        {
            return "결재권자에게 바로 요청합니다.";
        }

        if (stepCount.RequiresSecondApprover() == false)
        {
            return "검토 후 결재권자에게 요청합니다.";
        }

        return "1차, 2차 검토 후 결재권자에게 요청합니다.";
    }
}
