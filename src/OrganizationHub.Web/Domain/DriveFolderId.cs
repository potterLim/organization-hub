using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct DriveFolderId
{
    public const string ROOT_VALUE = "root";

    public string Value
    {
        get;
    }

    public bool IsRoot
    {
        get
        {
            return Value == ROOT_VALUE;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static DriveFolderId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static DriveFolderId Root
    {
        get
        {
            return CreateRequired(ROOT_VALUE);
        }
    }

    public static DriveFolderId CreateOptional(string? folderIdOrNull)
    {
        return new DriveFolderId(folderIdOrNull);
    }

    public static DriveFolderId CreateRequired(string? folderIdOrNull)
    {
        DriveFolderId folderId = CreateOptional(folderIdOrNull);
        if (folderId.HasValue == false)
        {
            throw new ArgumentException("Drive folder ID is required.", nameof(folderIdOrNull));
        }

        return folderId;
    }

    private DriveFolderId(string? folderIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(folderIdOrNull) ? string.Empty : folderIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Drive folder ID is required.");
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
