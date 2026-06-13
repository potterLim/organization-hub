using System;
using Google.Apis.Drive.v3;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService : IDriveService
{
    private const int MAX_CACHED_DOWNLOAD_BYTES = 20971520;

    private static readonly string[] DRIVE_SCOPES = new[] { DriveService.Scope.Drive };
    private static readonly TimeSpan FOLDER_LIST_CACHE_DURATION = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan FILE_METADATA_CACHE_DURATION = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ANCESTOR_CACHE_DURATION = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FILE_DOWNLOAD_CACHE_DURATION = TimeSpan.FromMinutes(2);

    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly GoogleApiOptions mOptions;
    private readonly IMemoryCache mMemoryCache;
    private readonly IChangeVersionService mChangeVersionService;

    public GoogleDriveService(IGoogleApiCredentialFactory credentialFactory, IOptions<GoogleApiOptions> options, IMemoryCache memoryCache, IChangeVersionService changeVersionService)
    {
        mCredentialFactory = credentialFactory;
        mOptions = options.Value;
        mMemoryCache = memoryCache;
        mChangeVersionService = changeVersionService;
    }

}
