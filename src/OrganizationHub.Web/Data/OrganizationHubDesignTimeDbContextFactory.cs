using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrganizationHub.Web.Data;

public sealed class OrganizationHubDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationHubDbContext>
{
    public OrganizationHubDbContext CreateDbContext(string[] args)
    {
        ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.SetBasePath(AppContext.BaseDirectory);
        configurationBuilder.AddJsonFile("appsettings.json", false);
        configurationBuilder.AddJsonFile("appsettings.Development.json", true);
        configurationBuilder.AddEnvironmentVariables();
        IConfigurationRoot configuration = configurationBuilder.Build();
        string? connectionStringOrNull = configuration.GetConnectionString("OrganizationHub");
        if (string.IsNullOrWhiteSpace(connectionStringOrNull))
        {
            connectionStringOrNull = "Data Source=Data/organizationhub.db";
        }

        DbContextOptionsBuilder<OrganizationHubDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationHubDbContext>();
        optionsBuilder.UseSqlite(connectionStringOrNull);
        return new OrganizationHubDbContext(optionsBuilder.Options);
    }
}
