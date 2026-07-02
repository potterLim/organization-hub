using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalComment
{
    public const int MAXIMUM_LENGTH = 1000;

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

    public bool IsWithinMaximumLength
    {
        get
        {
            return Value.Length <= MAXIMUM_LENGTH;
        }
    }

    public static ApprovalComment Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    private ApprovalComment(string comment)
    {
        mValueOrNull = comment;
    }

    public static ApprovalComment CreateOptional(string? commentOrNull)
    {
        if (string.IsNullOrWhiteSpace(commentOrNull))
        {
            return new ApprovalComment(string.Empty);
        }

        return new ApprovalComment(commentOrNull.Trim());
    }

    public static ApprovalComment CreateRequired(string? commentOrNull)
    {
        ApprovalComment comment = CreateOptional(commentOrNull);
        if (comment.HasValue == false)
        {
            throw new ArgumentException("Approval comment is required.", nameof(commentOrNull));
        }

        return comment;
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
