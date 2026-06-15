using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct FileContentType
{
    public const string DEFAULT_VALUE = "application/octet-stream";

    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return DEFAULT_VALUE;
            }

            return mValueOrNull;
        }
    }

    public static FileContentType Default
    {
        get
        {
            return new FileContentType(DEFAULT_VALUE);
        }
    }

    private FileContentType(string contentType)
    {
        mValueOrNull = contentType;
    }

    public static FileContentType CreateOptional(string? contentTypeOrNull)
    {
        if (string.IsNullOrWhiteSpace(contentTypeOrNull))
        {
            return Default;
        }

        return new FileContentType(contentTypeOrNull.Trim());
    }

    public string GetRequiredValue()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new InvalidOperationException("File content type is required.");
        }

        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
