using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepCountSelection
{
    private int Value
    {
        get;
    }

    public bool HasSupportedValue
    {
        get
        {
            return ApprovalStepCount.IsSupportedValue(Value);
        }
    }

    public bool RequiresFirstApprover()
    {
        return Value >= ApprovalStepCount.FIRST_APPROVER_REQUIRED_STEP_COUNT;
    }

    public bool RequiresSecondApprover()
    {
        return Value == ApprovalStepCount.SECOND_APPROVER_REQUIRED_STEP_COUNT;
    }

    public ApprovalStepCountSelection(int countValue)
    {
        Value = countValue;
    }

    public ApprovalStepCount GetRequiredStepCount()
    {
        if (HasSupportedValue == false)
        {
            throw new InvalidOperationException("Approval step count is outside the supported range.");
        }

        return new ApprovalStepCount(Value);
    }

    public int GetRequiredValue()
    {
        return GetRequiredStepCount().GetRequiredValue();
    }
}
