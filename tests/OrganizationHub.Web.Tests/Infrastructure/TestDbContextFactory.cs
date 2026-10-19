using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Tests.Infrastructure;

public sealed class TestDbContextFactory : IDbContextFactory<OrganizationHubDbContext>, IDisposable
{
    private readonly SqliteConnection mConnection;
    private readonly DbContextOptions<OrganizationHubDbContext> mDbContextOptions;
    private readonly string mDatabaseFilePath = string.Empty;

    public TestDbContextFactory()
    {
        mConnection = new SqliteConnection("Data Source=:memory:");
        mConnection.Open();
        DbContextOptionsBuilder<OrganizationHubDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationHubDbContext>();
        optionsBuilder.UseSqlite(mConnection);
        mDbContextOptions = optionsBuilder.Options;
        using (OrganizationHubDbContext dbContext = CreateDbContext())
        {
            dbContext.Database.EnsureCreated();
        }
    }

    public TestDbContextFactory(SaveChangesInterceptor interceptor)
    {
        mDatabaseFilePath = Path.Combine(Path.GetTempPath(), "organization-hub-test-" + Guid.NewGuid().ToString("N") + ".db");
        string connectionString = "Data Source=" + mDatabaseFilePath + ";Pooling=False;Default Timeout=30";
        mConnection = new SqliteConnection(connectionString);
        DbContextOptionsBuilder<OrganizationHubDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationHubDbContext>();
        optionsBuilder.UseSqlite(connectionString).AddInterceptors(interceptor);
        mDbContextOptions = optionsBuilder.Options;
        using (OrganizationHubDbContext dbContext = CreateDbContext())
        {
            dbContext.Database.EnsureCreated();
        }
    }

    public OrganizationHubDbContext CreateDbContext()
    {
        return new OrganizationHubDbContext(mDbContextOptions);
    }

    public ValueTask<OrganizationHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return new ValueTask<OrganizationHubDbContext>(CreateDbContext());
    }

    public void Dispose()
    {
        mConnection.Dispose();
        if (string.IsNullOrWhiteSpace(mDatabaseFilePath) == false)
        {
            File.Delete(mDatabaseFilePath);
            File.Delete(mDatabaseFilePath + "-wal");
            File.Delete(mDatabaseFilePath + "-shm");
        }
    }
}
