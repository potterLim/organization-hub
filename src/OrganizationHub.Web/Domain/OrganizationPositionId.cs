using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct OrganizationPositionId
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

    public static OrganizationPositionId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static OrganizationPositionId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Organization position ID cannot be negative.");
        }

        return new OrganizationPositionId(idValue);
    }

    public static OrganizationPositionId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Organization position ID must be greater than zero.");
        }

        return new OrganizationPositionId(idValue);
    }

    public static OrganizationPositionId? CreateExistingOrNull(int? idValueOrNull)
    {
        int idValue = idValueOrNull.GetValueOrDefault();
        if (idValue <= 0)
        {
            return null;
        }

        return CreateExisting(idValue);
    }

    public static int? GetValueOrNull(OrganizationPositionId? positionIdOrNull)
    {
        OrganizationPositionId positionId = positionIdOrNull.GetValueOrDefault();
        if (positionId.HasValue == false)
        {
            return null;
        }

        return positionId.GetRequiredValue();
    }

    private OrganizationPositionId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Organization position ID is required.");
        }

        return Value;
    }

    public int GetOptionalValue()
    {
        return Value;
    }
}
