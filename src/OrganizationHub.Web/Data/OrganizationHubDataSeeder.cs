using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDataSeeder
{
    private const string PREVIOUS_SEED_ADMINISTRATOR_DISPLAY_NAME = "Organization Hub Admin";
    private const int SQLITE_BUSY_TIMEOUT_MILLISECONDS = 5000;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPasswordHashService mPasswordHashService;
    private readonly OrganizationHubOptions mOptions;

    public OrganizationHubDataSeeder(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPasswordHashService passwordHashService,
        IOptions<OrganizationHubOptions> options
    )
    {
        mDbContextFactory = dbContextFactory;
        mPasswordHashService = passwordHashService;
        mOptions = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            await configureSqliteRuntimeAsync(dbContext, cancellationToken);

            bool hasGlobalSetting = await dbContext.GlobalSettings.AnyAsync(cancellationToken);
            if (hasGlobalSetting == false)
            {
                dbContext.GlobalSettings.Add(new GlobalSetting());
            }

            Team systemAdministrationTeam = await ensureSystemAdministrationTeamAsync(dbContext, cancellationToken);
            Team managementTeam = await ensureManagementTeamAsync(dbContext, cancellationToken);
            await ensureDefaultManagementPositionAsync(dbContext, managementTeam, cancellationToken);
            OrganizationPosition administratorPosition = await ensureSystemAdministratorPositionAsync(dbContext, systemAdministrationTeam, cancellationToken);

            bool hasUsers = await dbContext.UserAccounts.AnyAsync(cancellationToken);
            if (hasUsers == false)
            {
                UserAccount seedUser = createSeedUser(systemAdministrationTeam, administratorPosition);
                dbContext.UserAccounts.Add(seedUser);
            }

            await attachOrganizationAdministratorUsersToSystemAdministrationTeamAsync(dbContext, systemAdministrationTeam, cancellationToken);
            await attachUnassignedOrganizationMembersToManagementTeamAsync(dbContext, managementTeam, cancellationToken);
            await attachAdministratorsToSystemAdministratorPositionAsync(dbContext, administratorPosition, cancellationToken);
            await upgradeDefaultAdministratorDisplayNamesAsync(dbContext, cancellationToken);
            await seedNavigationMenusAsync(dbContext, cancellationToken);
            await seedWorkGuidelineSectionsAsync(dbContext, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task configureSqliteRuntimeAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(createSqliteBusyTimeoutCommand(), cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
    }

    private static string createSqliteBusyTimeoutCommand()
    {
        return "PRAGMA busy_timeout=" + SQLITE_BUSY_TIMEOUT_MILLISECONDS.ToString(CultureInfo.InvariantCulture) + ";";
    }

    private string getConfiguredSeedUsername()
    {
        string seedUsername = mOptions.SeedUsername;
        if (string.IsNullOrWhiteSpace(seedUsername) == false)
        {
            return seedUsername;
        }

        throw new InvalidOperationException("Seed username is required when the first Organization Hub account is created.");
    }

    private string getConfiguredSeedPassword()
    {
        string seedPassword = mOptions.SeedPassword;
        if (string.IsNullOrWhiteSpace(seedPassword) == false)
        {
            return seedPassword;
        }

        throw new InvalidOperationException("Seed password is required when the first Organization Hub account is created.");
    }

    private string getConfiguredSeedDisplayName()
    {
        if (string.IsNullOrWhiteSpace(mOptions.SeedDisplayName))
        {
            return "Organization Hub 관리자";
        }

        return mOptions.SeedDisplayName;
    }
}
