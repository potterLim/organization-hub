using System;
using System.Collections.Generic;
using System.IO;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal static class DriveFileNamePolicy
{
    private const int FIRST_DUPLICATE_FILE_NAME_NUMBER = 2;
    private const int FALLBACK_UNIQUE_SUFFIX_LENGTH = 8;
    private const int MAX_NUMBERED_FILE_NAME_ATTEMPT = 9999;

    public static DriveItemName CreateAvailableFileName(
        DriveItemName requestedFileName,
        IReadOnlyList<DriveFileItem> existingItems)
    {
        string requestedFileNameValue = requestedFileName.GetRequiredValue();
        HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DriveFileItem item in existingItems)
        {
            existingNames.Add(item.Name);
        }

        if (existingNames.Contains(requestedFileNameValue) == false)
        {
            return requestedFileName;
        }

        string extension = Path.GetExtension(requestedFileNameValue);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestedFileNameValue);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            fileNameWithoutExtension = requestedFileNameValue;
            extension = string.Empty;
        }

        for (int number = FIRST_DUPLICATE_FILE_NAME_NUMBER; number <= MAX_NUMBERED_FILE_NAME_ATTEMPT; number++)
        {
            string candidateFileName = fileNameWithoutExtension + " (" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" + extension;
            if (existingNames.Contains(candidateFileName) == false)
            {
                return DriveItemName.CreateRequired(candidateFileName);
            }
        }

        string fallbackFileName = fileNameWithoutExtension + " (" + Guid.NewGuid().ToString("N").Substring(0, FALLBACK_UNIQUE_SUFFIX_LENGTH) + ")" + extension;
        return DriveItemName.CreateRequired(fallbackFileName);
    }
}
