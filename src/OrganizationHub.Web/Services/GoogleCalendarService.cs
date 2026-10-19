using System;
using Google.Apis.Calendar.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService : ICalendarService
{
    private const string EVENT_FIELDS = "id,etag,summary,description,location,start,end,extendedProperties";
    private const string LOGICAL_CALENDAR_NAME_PROPERTY = "organizationHub.logicalCalendarName";
    private static readonly string[] CALENDAR_SCOPES = new string[] { CalendarService.Scope.Calendar };
    private static readonly TimeSpan EVENT_LIST_CACHE_DURATION = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CALENDAR_VERSION_CACHE_DURATION = TimeSpan.FromHours(1);

    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly GoogleApiOptions mOptions;
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IMemoryCache mMemoryCache;
    private readonly ILogger<GoogleCalendarService> mLogger;
    private readonly IChangeVersionService mChangeVersionService;
    private readonly IPermissionService mPermissionService;

    public GoogleCalendarService(
        IGoogleApiCredentialFactory credentialFactory,
        IOptions<GoogleApiOptions> options,
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IMemoryCache memoryCache,
        ILogger<GoogleCalendarService> logger,
        IChangeVersionService changeVersionService,
        IPermissionService permissionService)
    {
        mCredentialFactory = credentialFactory;
        mOptions = options.Value;
        mDbContextFactory = dbContextFactory;
        mMemoryCache = memoryCache;
        mLogger = logger;
        mChangeVersionService = changeVersionService;
        mPermissionService = permissionService;
    }

}
