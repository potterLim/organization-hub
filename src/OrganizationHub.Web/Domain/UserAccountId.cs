using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct UserAccountId
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

    public static UserAccountId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static UserAccountId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "User account ID cannot be negative.");
        }

        return new UserAccountId(idValue);
    }

    public static UserAccountId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "User account ID must be greater than zero.");
        }

        return new UserAccountId(idValue);
    }

    public static int? GetValueOrNull(UserAccountId? userAccountIdOrNull)
    {
        UserAccountId userAccountId = userAccountIdOrNull.GetValueOrDefault();
        if (userAccountId.HasValue == false)
        {
            return null;
        }

        return userAccountId.GetRequiredValue();
    }

    private UserAccountId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("User account ID is required.");
        }

        return Value;
    }

    public int GetOptionalValue()
    {
        return Value;
    }
}
