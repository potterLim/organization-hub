using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepCount
{
    public const int MINIMUM_VALUE = 1;
    public const int MAXIMUM_VALUE = 3;

    private int Value
    {
        get;
    }

    public bool IsSingleStep
    {
        get
        {
            return Value == MINIMUM_VALUE;
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

    public ApprovalStepCount(int countValue)
    {
        if (countValue < MINIMUM_VALUE || countValue > MAXIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(countValue), "Approval step count must be between 1 and 3.");
        }

        Value = countValue;
    }

    public int GetRequiredValue()
    {
        if (Value < MINIMUM_VALUE || Value > MAXIMUM_VALUE)
        {
            throw new InvalidOperationException("Approval step count is outside the supported range.");
        }

        return Value;
    }

    public int GetIntermediateApproverCount()
    {
        return GetRequiredValue() - 1;
    }

    public ApprovalStepNumber GetFinalStepNumber()
    {
        return new ApprovalStepNumber(GetRequiredValue());
    }

    public bool ContainsStepNumber(ApprovalStepNumber stepNumber)
    {
        return stepNumber.GetRequiredValue() <= GetRequiredValue();
    }
}
