using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalUserId
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

    public static ApprovalUserId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static ApprovalUserId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval user ID cannot be negative.");
        }

        return new ApprovalUserId(idValue);
    }

    public static ApprovalUserId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Approval user ID must be greater than zero.");
        }

        return new ApprovalUserId(idValue);
    }

    public static ApprovalUserId? CreateExistingOrNull(int? idValueOrNull)
    {
        int idValue = idValueOrNull.GetValueOrDefault();
        if (idValue <= 0)
        {
            return null;
        }

        return CreateExisting(idValue);
    }

    public static int? GetValueOrNull(ApprovalUserId? userIdOrNull)
    {
        ApprovalUserId userId = userIdOrNull.GetValueOrDefault();
        if (userId.HasValue == false)
        {
            return null;
        }

        return userId.GetRequiredValue();
    }

    private ApprovalUserId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Approval user ID is required.");
        }

        return Value;
    }
}
