using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalRequestId
{
    private int Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return Value > 0;
        }
    }

    public static ApprovalRequestId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static ApprovalRequestId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval request ID cannot be negative.");
        }

        return new ApprovalRequestId(idValue);
    }

    public static ApprovalRequestId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval request ID must be greater than zero.");
        }

        return new ApprovalRequestId(idValue);
    }

    private ApprovalRequestId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Approval request ID is required.");
        }

        return Value;
    }

    public int GetOptionalValue()
    {
        return Value;
    }
}
