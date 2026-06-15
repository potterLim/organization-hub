using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PasswordHash
{
    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return string.Empty;
            }

            return mValueOrNull;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    private PasswordHash(string passwordHash)
    {
        mValueOrNull = passwordHash;
    }

    public static PasswordHash CreateFromStoredValue(string? storedPasswordHashOrNull)
    {
        if (string.IsNullOrWhiteSpace(storedPasswordHashOrNull))
        {
            return new PasswordHash(string.Empty);
        }

        return new PasswordHash(storedPasswordHashOrNull.Trim());
    }

    public static PasswordHash CreateRequired(string passwordHashValue)
    {
        PasswordHash passwordHash = CreateFromStoredValue(passwordHashValue);
        if (passwordHash.HasValue == false)
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHashValue));
        }

        return passwordHash;
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Password hash is required.");
        }

        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
