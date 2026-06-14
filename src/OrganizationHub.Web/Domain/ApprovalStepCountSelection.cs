using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepCountSelection
{
    public int Value
    {
        get;
    }

    public bool HasSupportedValue
    {
        get
        {
            return Value >= ApprovalStepCount.MINIMUM_VALUE && Value <= ApprovalStepCount.MAXIMUM_VALUE;
        }
    }

    public bool RequiresFirstApprover
    {
        get
        {
            return Value >= 2;
        }
    }

    public bool RequiresSecondApprover
    {
        get
        {
            return Value == 3;
        }
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
}
