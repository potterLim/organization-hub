using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Tests.Infrastructure;

public sealed class TestDbContextFactory : IDbContextFactory<OrganizationHubDbContext>, IDisposable
{
    private readonly SqliteConnection mConnection;
    private readonly DbContextOptions<OrganizationHubDbContext> mDbContextOptions;

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
    }
}
