using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleCalendarData = Google.Apis.Calendar.v3.Data.Calendar;
using GoogleCalendarListData = Google.Apis.Calendar.v3.Data.CalendarList;
using GoogleCalendarListEntryData = Google.Apis.Calendar.v3.Data.CalendarListEntry;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string CALENDAR_LIST_CACHE_KEY = "OrganizationHub:GoogleWorkspace:Calendars";
    private static readonly string[] CALENDAR_SCOPES = new[] { GoogleCalendarApi.Scope.Calendar };

    public async Task<IReadOnlyList<GoogleCalendarAssignmentOption>> GetCalendarAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleCalendarAssignmentOption> options = new List<GoogleCalendarAssignmentOption>();
        options.Add(createCalendarAssignmentOption("Management", OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME + " Calendar", EGoogleCalendarAssignmentKind.Organization, null));
        options.Add(createCalendarAssignmentOption("Leader", OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " Calendar", EGoogleCalendarAssignmentKind.Leader, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                options.Add(createCalendarAssignmentOption("Team:" + team.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), team.DisplayName + " Calendar", EGoogleCalendarAssignmentKind.Team, team.ID));
            }
        }

        return options;
    }

    public async Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardCalendarsAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail("Calendar 생성 서비스를 사용할 수 없습니다.");
        }

        GoogleWorkspaceResourceSetupSummary summary = new GoogleWorkspaceResourceSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            GoogleCalendarApi calendarService = await createCalendarServiceAsync(cancellationToken);
            List<GoogleCalendarData> googleCalendars = await listGoogleCalendarsAsync(calendarService, cancellationToken);
            IReadOnlyList<GoogleCalendarAssignmentOption> options = await GetCalendarAssignmentOptionsAsync(cancellationToken);
            foreach (GoogleCalendarAssignmentOption option in options)
            {
                string standardName = getStandardCalendarName(option);
                GoogleCalendarData calendar = await getOrCreateGoogleCalendarAsync(calendarService, googleCalendars, standardName, summary, cancellationToken);
                if (string.IsNullOrWhiteSpace(calendar.Id))
                {
                    failureMessages.Add(standardName + " Calendar ID를 받지 못했습니다.");
                    continue;
                }

                ServiceResult assignResult = await AssignCalendarAsync(option.TargetKey, calendar.Id, cancellationToken);
                if (assignResult.IsSuccess)
                {
                    summary.AssignedCount = summary.AssignedCount + 1;
                    continue;
                }

                failureMessages.Add(standardName + " Calendar를 저장하지 못했습니다. " + assignResult.ErrorMessage);
            }

            clearCalendarListCache();
        }
        catch (Exception exception)
        {
            failureMessages.Add(createGoogleCalendarSetupErrorMessage(exception));
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail(createResourceSetupFailureMessage("Calendar", failureMessages));
        }

        return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Succeed(summary);
    }

    public async Task<ServiceResult> AssignCalendarAsync(string targetKey, string calendarID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return ServiceResult.Fail("저장 대상을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(calendarID))
        {
            return ServiceResult.Fail("Calendar를 선택해 주세요.");
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> options = await GetCalendarAssignmentOptionsAsync(cancellationToken);
        GoogleCalendarAssignmentOption? optionOrNull = options.FirstOrDefault(option => option.TargetKey == targetKey);
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("저장 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (optionOrNull.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.OrganizationCalendarID = calendarID.Trim();
            }
            else if (optionOrNull.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Leader)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.ResponsibleCalendarID = calendarID.Trim();
            }
            else
            {
                if (optionOrNull.TeamID.HasValue == false)
                {
                    return ServiceResult.Fail("팀 Calendar 대상을 찾을 수 없습니다.");
                }

                int teamID = optionOrNull.TeamID.Value;
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == teamID, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("팀을 찾을 수 없습니다.");
                }

                teamOrNull.CalendarID = calendarID.Trim();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private GoogleCalendarAssignmentOption createCalendarAssignmentOption(string targetKey, string displayName, EGoogleCalendarAssignmentKind calendarAssignmentKind, int? teamID)
    {
        GoogleCalendarAssignmentOption option = new GoogleCalendarAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.CalendarAssignmentKind = calendarAssignmentKind;
        option.TeamID = teamID;
        return option;
    }

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
            request.MaxResults = 250;
            request.PageToken = string.IsNullOrWhiteSpace(pageToken) ? null : pageToken;
            request.Fields = "items(id,summary),nextPageToken";
            GoogleCalendarListData response = await request.ExecuteAsync(cancellationToken);
            if (response.Items != null)
            {
                foreach (GoogleCalendarListEntryData calendarEntry in response.Items)
                {
                    GoogleCalendarData calendar = new GoogleCalendarData();
                    calendar.Id = getNullAsEmpty(calendarEntry.Id);
                    calendar.Summary = getNullAsEmpty(calendarEntry.Summary);
                    calendars.Add(calendar);
                }
            }

            pageToken = getNullAsEmpty(response.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        return calendars;
    }

    private async Task<GoogleCalendarData> getOrCreateGoogleCalendarAsync(GoogleCalendarApi calendarService, List<GoogleCalendarData> googleCalendars, string standardName, GoogleWorkspaceResourceSetupSummary summary, CancellationToken cancellationToken)
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
        newCalendar.TimeZone = getCalendarTimeZoneID();
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

        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Leader)
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

    private string getCalendarTimeZoneID()
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
        return GoogleApiExceptionMessageFormatter.CreateGoogleCalendarMessage(exception, "표준 Calendar를 만들지 못했습니다.");
    }
}
