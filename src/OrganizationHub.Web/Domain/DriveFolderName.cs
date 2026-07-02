using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct DriveFolderName
{
    public const int MAXIMUM_LENGTH = 80;

    private const char DIRECTORY_SEPARATOR_CHARACTER = '/';
    private const char WINDOWS_DIRECTORY_SEPARATOR_CHARACTER = '\\';

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

    public static DriveFolderName Empty
    {
        get
        {
            return new DriveFolderName(string.Empty);
        }
    }

    private DriveFolderName(string folderName)
    {
        mValueOrNull = folderName;
    }

    public static DriveFolderName CreateOptional(string? folderNameOrNull)
    {
        if (string.IsNullOrWhiteSpace(folderNameOrNull))
        {
            return Empty;
        }

        string folderName = folderNameOrNull.Trim();
        if (folderName.Length > MAXIMUM_LENGTH)
        {
            return Empty;
        }

        if (folderName.Contains(DIRECTORY_SEPARATOR_CHARACTER) || folderName.Contains(WINDOWS_DIRECTORY_SEPARATOR_CHARACTER))
        {
            return Empty;
        }

        return new DriveFolderName(folderName);
    }

    public static DriveFolderName CreateRequired(string? folderNameOrNull)
    {
        DriveFolderName folderName = CreateOptional(folderNameOrNull);
        if (folderName.HasValue == false)
        {
            throw new ArgumentException("Drive folder name is required.", nameof(folderNameOrNull));
        }

        return folderName;
    }

    public DriveItemName ToDriveItemName()
    {
        return DriveItemName.CreateRequired(GetRequiredValue());
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Drive folder name is required.");
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
