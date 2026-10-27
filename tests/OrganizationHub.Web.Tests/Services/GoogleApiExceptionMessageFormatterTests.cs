using System;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class GoogleApiExceptionMessageFormatterTests
{
    private const string MESSAGE = "목록을 불러오지 못했습니다. 다시 불러와 주세요.";

    [Theory]
    [InlineData("drive")]
    [InlineData("calendar")]
    [InlineData("tasks")]
    public void UnexpectedFailuresDoNotExposeExceptionDetails(string area)
    {
        Func<Exception, string, string> format = getFormatter(area);
        Assert.Equal(MESSAGE, format(new InvalidOperationException("Internal database and request details"), MESSAGE));
    }

    [Theory]
    [InlineData("drive")]
    [InlineData("calendar")]
    [InlineData("tasks")]
    public void AdministratorsRetainTheActionNeededToRestoreAuthorization(string area)
    {
        Func<Exception, string, string> format = getFormatter(area);
        Assert.Contains("다시 연결", format(new InvalidOperationException("insufficient authentication scopes"), "조회 실패"));
    }

    private static Func<Exception, string, string> getFormatter(string area)
    {
        switch (area)
        {
            case "drive":
                return GoogleApiExceptionMessageFormatter.CreateGoogleDriveMessage;
            case "calendar":
                return GoogleApiExceptionMessageFormatter.CreateGoogleCalendarMessage;
            default:
                return GoogleApiExceptionMessageFormatter.CreateGoogleTasksMessage;
        }
    }
}
