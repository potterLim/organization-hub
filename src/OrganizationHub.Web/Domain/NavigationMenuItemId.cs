using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct NavigationMenuItemId
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

    public static NavigationMenuItemId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static NavigationMenuItemId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Navigation menu item ID cannot be negative.");
        }

        return new NavigationMenuItemId(idValue);
    }

    public static NavigationMenuItemId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Navigation menu item ID must be greater than zero.");
        }

        return new NavigationMenuItemId(idValue);
    }

    private NavigationMenuItemId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Navigation menu item ID is required.");
        }

        return Value;
    }
}
