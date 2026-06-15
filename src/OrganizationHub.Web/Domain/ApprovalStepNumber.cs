using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepNumber
{
    public const int MINIMUM_VALUE = 1;

    private int Value
    {
        get;
    }

    public ApprovalStepNumber(int numberValue)
    {
        if (numberValue < MINIMUM_VALUE || numberValue > ApprovalStepCount.MAXIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(numberValue), "Approval step number must be between 1 and 3.");
        }

        Value = numberValue;
    }

    public int GetRequiredValue()
    {
        if (Value < MINIMUM_VALUE || Value > ApprovalStepCount.MAXIMUM_VALUE)
        {
            throw new InvalidOperationException("Approval step number is outside the supported range.");
        }

        return Value;
    }

    public static int? GetValueOrNull(ApprovalStepNumber? stepNumberOrNull)
    {
        if (stepNumberOrNull.HasValue == false)
        {
            return null;
        }

        return stepNumberOrNull.GetValueOrDefault().GetRequiredValue();
    }

    public bool IsFinalStepOf(ApprovalStepCount stepCount)
    {
        return GetRequiredValue() == stepCount.GetRequiredValue();
    }
}
