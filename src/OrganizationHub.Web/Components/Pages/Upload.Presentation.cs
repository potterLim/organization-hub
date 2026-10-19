using System;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private const string FILE_SIZE_DECIMAL_FORMAT = "0.0";

    private FileUploadPolicy? getCurrentUploadPolicyOrNull()
    {
        DriveFolderReference? rootFolderOrNull = getSelectedRootFolderOrNull();
        if (rootFolderOrNull == null)
        {
            return null;
        }

        EFileUploadPolicyContext policyContext = isCurrentPathMinutesFolder() ? EFileUploadPolicyContext.MinutesFolder : EFileUploadPolicyContext.RegularFolder;
        return FilePolicyService.GetUploadPolicy(rootFolderOrNull.FolderKind, policyContext);
    }

    private string getUploadPolicyText()
    {
        FileUploadPolicy? policyOrNull = getCurrentUploadPolicyOrNull();
        if (policyOrNull == null)
        {
            return "파일을 올릴 위치를 먼저 선택해 주세요.";
        }

        return "허용 형식: " + string.Join(", ", policyOrNull.AllowedExtensions.OrderBy(extension => extension)) + " · 최대 " + formatUploadLimit(policyOrNull.MaximumUploadBytes);
    }

    private static string formatUploadLimit(long maximumUploadBytes)
    {
        return (maximumUploadBytes / (decimal)OrganizationHubFileLimits.BYTES_PER_MEGABYTE).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
    }

    private void showMessage(string message, Severity severity)
    {
        mMessage = message;
        mMessageSeverity = severity;
    }

    private void showFolderMessage(string message, Severity severity)
    {
        mFolderMessage = message;
        mFolderMessageSeverity = severity;
    }

    private void clearFolderMessage()
    {
        mFolderMessage = string.Empty;
    }

    private string getCurrentFolderDisplayName()
    {
        if (mFolderPath.Count == 0)
        {
            return "선택 위치";
        }

        return string.Join(" / ", mFolderPath.Select(pathItem => pathItem.Name.Trim()).Where(pathName => string.IsNullOrWhiteSpace(pathName) == false));
    }

    private string formatDate(DateTime dateTime)
    {
        return PresentationDateTimeFormatter.formatClientDateTime(dateTime, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }

    private string formatOptionalDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return PresentationDateTimeFormatter.formatClientDateTime(dateTimeOrNull.Value, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }

    private string formatFileSize(long size)
    {
        if (size < OrganizationHubFileLimits.BYTES_PER_KILOBYTE)
        {
            return size.ToString(CultureInfo.InvariantCulture) + " B";
        }

        if (size < OrganizationHubFileLimits.BYTES_PER_MEGABYTE)
        {
            return (size / (double)OrganizationHubFileLimits.BYTES_PER_KILOBYTE).ToString(FILE_SIZE_DECIMAL_FORMAT, CultureInfo.InvariantCulture) + " KB";
        }

        return (size / (double)OrganizationHubFileLimits.BYTES_PER_MEGABYTE).ToString(FILE_SIZE_DECIMAL_FORMAT, CultureInfo.InvariantCulture) + " MB";
    }

    private string getFolderCountText()
    {
        return "폴더 " + mFoldersInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + mFilesInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private bool shouldShowFolderPath()
    {
        return mFolderPath.Count > 1;
    }

    private string getSelectedFileText()
    {
        if (mSelectedFileOrNull == null)
        {
            return "선택된 파일 없음";
        }

        return mSelectedFileOrNull.Name + " · " + formatFileSize(mSelectedFileOrNull.Size);
    }

    private string getUploadConflictTitle()
    {
        if (mUploadConflictOrNull == null)
        {
            return string.Empty;
        }

        return "같은 이름의 파일이 " + mUploadConflictOrNull.ExistingFiles.Count.ToString(CultureInfo.InvariantCulture) + "개 있습니다.";
    }

    private string getUploadSuggestedFileName()
    {
        if (mUploadConflictOrNull == null)
        {
            return string.Empty;
        }

        return mUploadConflictOrNull.SuggestedNewFileName.GetRequiredValue();
    }
}
