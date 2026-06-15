using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct DriveItemName
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

    public static DriveItemName Empty
    {
        get
        {
            return new DriveItemName(string.Empty);
        }
    }

    private DriveItemName(string itemName)
    {
        mValueOrNull = itemName;
    }

    public static DriveItemName CreateOptional(string? itemNameOrNull)
    {
        if (string.IsNullOrWhiteSpace(itemNameOrNull))
        {
            return Empty;
        }

        string itemName = itemNameOrNull.Trim();
        return new DriveItemName(itemName);
    }

    public static DriveItemName CreateRequired(string? itemNameOrNull)
    {
        DriveItemName itemName = CreateOptional(itemNameOrNull);
        if (itemName.HasValue == false)
        {
            throw new ArgumentException("Drive item name is required.", nameof(itemNameOrNull));
        }

        return itemName;
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Drive item name is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
