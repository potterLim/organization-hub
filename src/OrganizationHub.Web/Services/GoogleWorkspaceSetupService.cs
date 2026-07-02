using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService : IGoogleWorkspaceSetupService
{
    private const int GOOGLE_CALENDAR_LIST_PAGE_SIZE = 250;
    private const int GOOGLE_TASK_LIST_PAGE_SIZE = 100;
    private const int VISIBLE_FAILURE_MESSAGE_COUNT = 3;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IDriveService mDriveService;
    private readonly IGoogleApiCredentialFactory? mCredentialFactoryOrNull;
    private readonly GoogleApiOptions? mOptionsOrNull;
    private readonly IMemoryCache? mMemoryCacheOrNull;

    public GoogleWorkspaceSetupService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IDriveService driveService)
        : this(dbContextFactory, driveService, (IGoogleApiCredentialFactory?)null, (GoogleApiOptions?)null, (IMemoryCache?)null)
    {
    }

    public GoogleWorkspaceSetupService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDriveService driveService,
        IGoogleApiCredentialFactory credentialFactory,
        IOptions<GoogleApiOptions> options,
        IMemoryCache memoryCache
    )
        : this(dbContextFactory, driveService, credentialFactory, options.Value, memoryCache)
    {
    }

    private GoogleWorkspaceSetupService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDriveService driveService,
        IGoogleApiCredentialFactory? credentialFactoryOrNull,
        GoogleApiOptions? optionsOrNull,
        IMemoryCache? memoryCacheOrNull
    )
    {
        mDbContextFactory = dbContextFactory;
        mDriveService = driveService;
        mCredentialFactoryOrNull = credentialFactoryOrNull;
        mOptionsOrNull = optionsOrNull;
        mMemoryCacheOrNull = memoryCacheOrNull;
    }

    private async Task<List<Team>> getWorkspaceTeamsAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Teams
            .Where(team => team.IsActive && team.TeamKey != OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY)
            .OrderBy(team => team.SortOrder)
            .ThenBy(team => team.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private string createResourceSetupFailureMessage(string resourceName, IReadOnlyList<string> failureMessages)
    {
        List<string> visibleMessages = failureMessages.Take(VISIBLE_FAILURE_MESSAGE_COUNT).ToList();
        string message = "일부 " + resourceName + " 리소스를 준비하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }

    private async Task<GlobalSetting> getGlobalSettingAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
        if (settingOrNull != null)
        {
            return settingOrNull;
        }

        GlobalSetting setting = new GlobalSetting();
        dbContext.GlobalSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
