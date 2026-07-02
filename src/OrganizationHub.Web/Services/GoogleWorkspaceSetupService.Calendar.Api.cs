using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleCalendarData = Google.Apis.Calendar.v3.Data.Calendar;
using GoogleCalendarListData = Google.Apis.Calendar.v3.Data.CalendarList;
using GoogleCalendarListEntryData = Google.Apis.Calendar.v3.Data.CalendarListEntry;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private async Task<GoogleCalendarApi> createCalendarServiceAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            throw new InvalidOperationException("Calendar 생성 서비스를 사용할 수 없습니다.");
        }

        IConfigurableHttpClientInitializer credential = await mCredentialFactoryOrNull.CreateHttpClientInitializerAsync(CALENDAR_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptionsOrNull.ApplicationName;
        return new GoogleCalendarApi(initializer);
    }

    private async Task<List<GoogleCalendarData>> listGoogleCalendarsAsync(GoogleCalendarApi calendarService, CancellationToken cancellationToken)
    {
        List<GoogleCalendarData> calendars = new List<GoogleCalendarData>();
        string pageToken = string.Empty;
        do
        {
            CalendarListResource.ListRequest request = calendarService.CalendarList.List();
            request.MaxResults = GOOGLE_CALENDAR_LIST_PAGE_SIZE;
            request.PageToken = string.IsNullOrWhiteSpace(pageToken) ? null : pageToken;
            request.Fields = "items(id,summary),nextPageToken";
            GoogleCalendarListData response = await request.ExecuteAsync(cancellationToken);
            if (response.Items != null)
            {
                foreach (GoogleCalendarListEntryData calendarEntry in response.Items)
                {
                    GoogleCalendarData calendar = new GoogleCalendarData();
                    calendar.Id = getTextOrEmpty(calendarEntry.Id);
                    calendar.Summary = getTextOrEmpty(calendarEntry.Summary);
                    calendars.Add(calendar);
                }
            }

            pageToken = getTextOrEmpty(response.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        return calendars;
    }

    private async Task<GoogleCalendarData> getOrCreateGoogleCalendarAsync(
        GoogleCalendarApi calendarService,
        List<GoogleCalendarData> googleCalendars,
        string standardName,
        GoogleWorkspaceResourceSetupSummary summary,
        CancellationToken cancellationToken)
    {
        summary.TargetCount = summary.TargetCount + 1;
        GoogleCalendarData? existingCalendarOrNull = googleCalendars.FirstOrDefault(calendar => calendar.Summary == standardName);
        if (existingCalendarOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingCalendarOrNull;
        }

        GoogleCalendarData newCalendar = new GoogleCalendarData();
        newCalendar.Summary = standardName;
        newCalendar.TimeZone = getCalendarTimeZoneId();
        CalendarsResource.InsertRequest request = calendarService.Calendars.Insert(newCalendar);
        GoogleCalendarData createdCalendar = await request.ExecuteAsync(cancellationToken);
        googleCalendars.Add(createdCalendar);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdCalendar;
    }

    private string getStandardCalendarName(GoogleCalendarAssignmentOption option)
    {
        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
        {
            return OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME;
        }

        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Responsible)
        {
            return OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME;
        }

        string displayName = option.DisplayName;
        if (displayName.EndsWith(" Calendar", StringComparison.Ordinal))
        {
            displayName = displayName.Substring(0, displayName.Length - " Calendar".Length);
        }

        return displayName;
    }

    private string getCalendarTimeZoneId()
    {
        if (mOptionsOrNull == null || string.IsNullOrWhiteSpace(mOptionsOrNull.CalendarTimeZone))
        {
            return "Asia/Seoul";
        }

        return mOptionsOrNull.CalendarTimeZone;
    }

    private void clearCalendarListCache()
    {
        if (mMemoryCacheOrNull == null)
        {
            return;
        }

        mMemoryCacheOrNull.Remove(CALENDAR_LIST_CACHE_KEY);
    }

    private string createGoogleCalendarSetupErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleCalendarMessage(exception, "기본 Calendar를 만들지 못했습니다.");
    }
}
