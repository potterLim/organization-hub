using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalWorkflowId
{
    public int Value
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

    public static ApprovalWorkflowId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static ApprovalWorkflowId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval workflow ID cannot be negative.");
        }

        return new ApprovalWorkflowId(idValue);
    }

    public static ApprovalWorkflowId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval workflow ID must be greater than zero.");
        }

        return new ApprovalWorkflowId(idValue);
    }

    private ApprovalWorkflowId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Approval workflow ID is required.");
        }

        return Value;
    }
}
