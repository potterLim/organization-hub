using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct WorkGuidelineSectionId
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

    public static WorkGuidelineSectionId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static WorkGuidelineSectionId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Work guideline section ID cannot be negative.");
        }

        return new WorkGuidelineSectionId(idValue);
    }

    public static WorkGuidelineSectionId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Work guideline section ID must be greater than zero.");
        }

        return new WorkGuidelineSectionId(idValue);
    }

    private WorkGuidelineSectionId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Work guideline section ID is required.");
        }

        return Value;
    }

    public int GetOptionalValue()
    {
        return Value;
    }
}
