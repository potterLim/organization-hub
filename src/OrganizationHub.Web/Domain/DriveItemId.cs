using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct DriveItemId
{
    private string Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static DriveItemId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static DriveItemId CreateOptional(string? itemIdOrNull)
    {
        return new DriveItemId(itemIdOrNull);
    }

    public static DriveItemId CreateRequired(string? itemIdOrNull)
    {
        DriveItemId itemId = CreateOptional(itemIdOrNull);
        if (itemId.HasValue == false)
        {
            throw new ArgumentException("Drive item ID is required.", nameof(itemIdOrNull));
        }

        return itemId;
    }

    private DriveItemId(string? itemIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(itemIdOrNull) ? string.Empty : itemIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Drive item ID is required.");
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
