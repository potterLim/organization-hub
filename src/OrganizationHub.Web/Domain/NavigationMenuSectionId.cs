using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct NavigationMenuSectionId
{
    private readonly int mValue;

    public bool HasValue
    {
        get
        {
            return mValue > 0;
        }
    }

    private NavigationMenuSectionId(int value)
    {
        mValue = value;
    }

    public static NavigationMenuSectionId CreateOptional(int value)
    {
        if (value <= 0)
        {
            return Empty;
        }

        return new NavigationMenuSectionId(value);
    }

    public static NavigationMenuSectionId CreateExisting(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Navigation menu section id must be greater than zero.");
        }

        return new NavigationMenuSectionId(value);
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Navigation menu section id is required.");
        }

        return mValue;
    }

    public static NavigationMenuSectionId Empty
    {
        get
        {
            return new NavigationMenuSectionId(0);
        }
    }
}
