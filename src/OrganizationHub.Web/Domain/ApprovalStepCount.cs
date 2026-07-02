using System;
using System.Collections.Generic;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepCount
{
    public const int MINIMUM_VALUE = 1;
    public const int FIRST_APPROVER_REQUIRED_STEP_COUNT = 2;
    public const int SECOND_APPROVER_REQUIRED_STEP_COUNT = 3;
    public const int MAXIMUM_VALUE = 3;
    public const int DEFAULT_VALUE = FIRST_APPROVER_REQUIRED_STEP_COUNT;

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

    public bool RequiresFirstApprover()
    {
        return Value >= FIRST_APPROVER_REQUIRED_STEP_COUNT;
    }

    public bool RequiresSecondApprover()
    {
        return Value == SECOND_APPROVER_REQUIRED_STEP_COUNT;
    }

    public static bool IsSupportedValue(int countValue)
    {
        return countValue >= MINIMUM_VALUE && countValue <= MAXIMUM_VALUE;
    }

    public static ApprovalStepCount CreateDefault()
    {
        return new ApprovalStepCount(DEFAULT_VALUE);
    }

    public static IReadOnlyList<ApprovalStepCount> GetSupportedStepCounts()
    {
        return new ApprovalStepCount[]
        {
            new ApprovalStepCount(MINIMUM_VALUE),
            new ApprovalStepCount(FIRST_APPROVER_REQUIRED_STEP_COUNT),
            new ApprovalStepCount(SECOND_APPROVER_REQUIRED_STEP_COUNT)
        };
    }

    public ApprovalStepCount(int countValue)
    {
        if (IsSupportedValue(countValue) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(countValue), "Approval step count must be between 1 and 3.");
        }

        Value = countValue;
    }

    public int GetRequiredValue()
    {
        if (IsSupportedValue(Value) == false)
        {
            throw new InvalidOperationException("Approval step count is outside the supported range.");
        }

        return Value;
    }

    public int GetIntermediateApproverCount()
    {
        return GetRequiredValue() - MINIMUM_VALUE;
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
