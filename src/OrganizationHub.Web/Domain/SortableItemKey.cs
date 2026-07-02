using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct SortableItemKey
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

    public static SortableItemKey Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static SortableItemKey CreateOptional(string? itemKeyOrNull)
    {
        return new SortableItemKey(itemKeyOrNull);
    }

    public static SortableItemKey CreateRequired(string? itemKeyOrNull)
    {
        SortableItemKey itemKey = CreateOptional(itemKeyOrNull);
        if (itemKey.HasValue == false)
        {
            throw new ArgumentException("Sortable item key is required.", nameof(itemKeyOrNull));
        }

        return itemKey;
    }

    private SortableItemKey(string? itemKeyOrNull)
    {
        Value = string.IsNullOrWhiteSpace(itemKeyOrNull) ? string.Empty : itemKeyOrNull.Trim();
    }

    public bool StartsWith(string prefix)
    {
        return Value.StartsWith(prefix, StringComparison.Ordinal);
    }

    public string RemovePrefix(string prefix)
    {
        if (StartsWith(prefix) == false)
        {
            return string.Empty;
        }

        return Value[prefix.Length..];
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Sortable item key is required.");
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
