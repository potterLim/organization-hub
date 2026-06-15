using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct OrganizationTeamId
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

    public static OrganizationTeamId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static OrganizationTeamId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Organization team ID cannot be negative.");
        }

        return new OrganizationTeamId(idValue);
    }

    public static OrganizationTeamId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Organization team ID must be greater than zero.");
        }

        return new OrganizationTeamId(idValue);
    }

    public static OrganizationTeamId? CreateExistingOrNull(int? idValueOrNull)
    {
        int idValue = idValueOrNull.GetValueOrDefault();
        if (idValue <= 0)
        {
            return null;
        }

        return CreateExisting(idValue);
    }

    public static int? GetValueOrNull(OrganizationTeamId? teamIdOrNull)
    {
        OrganizationTeamId teamId = teamIdOrNull.GetValueOrDefault();
        if (teamId.HasValue == false)
        {
            return null;
        }

        return teamId.GetRequiredValue();
    }

    private OrganizationTeamId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Organization team ID is required.");
        }

        return Value;
    }

    public int GetOptionalValue()
    {
        return Value;
    }
}
