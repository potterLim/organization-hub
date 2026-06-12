using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class OrganizationHubDataSeederTests
{
    [Fact]
    public async Task FreshProductionDatabaseCreatesSystemAndManagementBootstrapAsync()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), "organizationhub-seed-" + Path.GetRandomFileName() + ".db");
        try
        {
            DbContextOptionsBuilder<OrganizationHubDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            optionsBuilder.UseSqlite("Data Source=" + databasePath + ";Cache=Shared;Default Timeout=30;Pooling=False");
            DbContextFactory dbContextFactory = new DbContextFactory(optionsBuilder.Options);
            OrganizationHubOptions options = new OrganizationHubOptions();
            options.SeedUsername = "admin";
            options.SeedPassword = "ChangeMe!2026";
            options.SeedDisplayName = "관리자";
            OrganizationHubDataSeeder seeder = new OrganizationHubDataSeeder(dbContextFactory, new FakePasswordHashService(), Options.Create(options), new FakeWebHostEnvironment());

            await seeder.SeedAsync(CancellationToken.None);

            await using (OrganizationHubDbContext dbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None))
            {
                List<string> teamNames = await dbContext.Teams.OrderBy(team => team.SortOrder).Select(team => team.DisplayName).ToListAsync();
                Assert.Equal(
                    new[] { OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME },
                    teamNames);

                UserAccount admin = await dbContext.UserAccounts.Include(user => user.Position).SingleAsync(user => user.Username == "admin");
                Assert.Equal(EUserRole.Admin, admin.Role);
                Assert.NotNull(admin.Position);
                if (admin.Position != null)
                {
                    Assert.True(admin.Position.IsSystemAdministrator);
                }
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(databasePath);
            }
            deleteIfExists(databasePath + "-shm");
            deleteIfExists(databasePath + "-wal");
        }
    }

    private static void deleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class DbContextFactory : IDbContextFactory<OrganizationHubDbContext>
    {
        private readonly DbContextOptions<OrganizationHubDbContext> mOptions;

        public DbContextFactory(DbContextOptions<OrganizationHubDbContext> options)
        {
            mOptions = options;
        }

        public OrganizationHubDbContext CreateDbContext()
        {
            return new OrganizationHubDbContext(mOptions);
        }

        public ValueTask<OrganizationHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<OrganizationHubDbContext>(CreateDbContext());
        }
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password)
        {
            return "hashed:" + password;
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return passwordHash == "hashed:" + password;
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "OrganizationHub.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Production";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
