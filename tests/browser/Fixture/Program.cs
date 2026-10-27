using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;

namespace OrganizationHub.BrowserFixture;

public static class Program
{
    public static void Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("HUB_FIXTURE_ACK") != "ISOLATED_ONLY")
        {
            throw new InvalidOperationException("Set HUB_FIXTURE_ACK=ISOLATED_ONLY for synthetic local testing.");
        }

        string password = Environment.GetEnvironmentVariable("HUB_TEST_PASSWORD")!;
        string appRoot = Environment.GetEnvironmentVariable("HUB_APP_CONTENT_ROOT")!;
        string databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("HUB_FIXTURE_DATABASE")!);
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(appRoot) || File.Exists(databasePath))
        {
            throw new InvalidOperationException("A password, published content root, and a fresh database path are required.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(App).Assembly.GetName().Name, ContentRootPath = appRoot, EnvironmentName = Environments.Development, Args = args });
        string? configuredUrlsOrNull = builder.Configuration["urls"];
        string urls = string.IsNullOrWhiteSpace(configuredUrlsOrNull) ? "http://127.0.0.1:5202" : configuredUrlsOrNull;
        Uri? addressOrNull;
        foreach (string url in urls.Split(';'))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out addressOrNull) == false || addressOrNull.IsLoopback == false)
            {
                throw new InvalidOperationException("The synthetic fixture must bind only to loopback addresses.");
            }
        }
        builder.WebHost.UseUrls(urls);
        builder.Configuration["ConnectionStrings:OrganizationHub"] = "Data Source=" + databasePath;
        builder.Configuration["OrganizationHub:SeedUsername"] = "fixture-seed";
        builder.Configuration["OrganizationHub:SeedPassword"] = password;
        Type program = typeof(OrganizationHub.Web.Program);
        program.GetMethod("configureServices", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { builder });
        foreach (ServiceDescriptor descriptor in builder.Services.Where(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType?.Namespace == "OrganizationHub.Web.Services").ToList())
        {
            builder.Services.Remove(descriptor);
        }
        builder.Services.AddScoped<IGoogleApiCredentialFactory, CaptureGoogleTransport>();
        builder.Services.AddScoped<IGoogleConnectionService, CaptureConnection>();
        builder.Services.AddScoped<IGoogleHealthCheckService, CaptureHealth>();
        builder.Services.AddScoped<IDriveService>(provider => DispatchProxy.Create<IDriveService, CaptureDriveProxy>());
        WebApplication app = builder.Build();
        program.GetMethod("seedDatabase", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { app });
        using (IServiceScope scope = app.Services.CreateScope())
        using (OrganizationHubDbContext database = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrganizationHubDbContext>>().CreateDbContext())
        {
            database.Database.OpenConnection();
            using (System.Data.Common.DbCommand command = database.Database.GetDbConnection().CreateCommand())
            {
                string hash = scope.ServiceProvider.GetRequiredService<IPasswordHashService>().HashPassword(PlainPassword.CreateFromInput(password)).GetRequiredValue();
                command.CommandText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seed.sql")).Replace("FIXTURE_PASSWORD_HASH", hash).Replace("FIXTURE_DOCUMENT_HEX", Convert.ToHexString(CaptureState.DocumentContent));
                command.ExecuteNonQuery();
            }
        }
        program.GetMethod("configurePipeline", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { app });
        app.MapPost("/fixture/state/{mode}", async (string mode, IDbContextFactory<OrganizationHubDbContext> factory, IMemoryCache cache) =>
        {
            CaptureState.Mode = mode;
            ((MemoryCache)cache).Compact(1);
            using (OrganizationHubDbContext db = await factory.CreateDbContextAsync())
            {
                GlobalSetting setting = await db.GlobalSettings.SingleAsync();
                bool configured = mode != "unconfigured";
                setting.OfficialRootFolderId = configured && mode != "root-unconfigured" ? "qa-root" : null;
                setting.CommonFolderId = configured ? "qa-common" : null;
                setting.PublicFolderId = configured ? "qa-public" : null;
                setting.ResponsibleResourcesFolderId = configured ? "qa-responsible" : null;
                setting.WorkResourcesFolderId = configured ? "qa-work" : null;
                setting.OrganizationCalendarId = configured ? "qa-calendar" : null;
                setting.OrganizationTaskListId = configured ? "qa-tasks" : null;
                Team team = await db.Teams.SingleAsync(item => item.Id == 2);
                team.MaterialsFolderId = configured ? "qa-team" : null;
                team.ApprovedFolderId = configured ? "qa-approved" : null;
                team.CalendarId = configured ? "team-calendar" : null;
                team.TaskListId = configured ? "team-tasks" : null;
                foreach (ApprovalWorkflowStep step in await db.ApprovalWorkflowSteps.Where(step => step.ApprovalWorkflowId == 1 && step.StepOrder == 2).ToListAsync())
                {
                    step.ApproverUserId = mode == "self-approver" ? 1 : mode == "missing-approver" ? 10 : 7;
                }
                await db.SaveChangesAsync();
            }
            return Results.Ok(new
            {
                mode
            });
        });
        app.MapPost("/fixture/reset-resources", (IMemoryCache cache) => { CaptureState.Drive = CaptureState.CreateDrive(); CaptureGoogleTransport.Reset(); ((MemoryCache)cache).Compact(1); return Results.Ok(); });
        app.MapPost("/fixture/revoke/{id:int}", async (int id, IDbContextFactory<OrganizationHubDbContext> factory) => { using (OrganizationHubDbContext db = await factory.CreateDbContextAsync()) { UserAccount user = await db.UserAccounts.SingleAsync(item => item.Id == id); user.AuthenticationVersion++; await db.SaveChangesAsync(); } return Results.Ok(); });

        app.MapGet("/fixture/records", async (IDbContextFactory<OrganizationHubDbContext> factory) =>
        {
            using (OrganizationHubDbContext db = await factory.CreateDbContextAsync())
            {
                return Results.Ok(new
                {
                    pendingUploads = await db.PendingUploadAudits.CountAsync(),
                    teamFiles = (await CaptureState.Drive.ListFilesAsync(DriveFolderId.CreateRequired("qa-team"), CancellationToken.None)).Select(item => new { fileId = item.Id, fileName = item.Name }).ToList(),
                    uploads = await db.UploadAuditLogs.Select(item => new { fileId = item.FileId, fileName = item.FileName }).ToListAsync(),
                    latestDeletionId = await db.CalendarEventDeletionAuditLogs.MaxAsync(item => item.Id),
                    latestDeletion = (await db.CalendarEventDeletionAuditLogs.OrderByDescending(item => item.Id).Select(item => item.Status).FirstOrDefaultAsync()).ToString()
                });
            }
        });
        app.MapPost("/fixture/publish/{id:int}", async (int id, IApprovalPublicationService publication) =>
            Results.Ok(await publication.PublishApprovedFileAsync(ApprovalRequestId.CreateExisting(id), CancellationToken.None)));

        app.MapPost("/fixture/fault/{mode}", async (string mode, IDbContextFactory<OrganizationHubDbContext> factory) =>
        {
            using (OrganizationHubDbContext db = await factory.CreateDbContextAsync())
            {
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS audit_todo; DROP TRIGGER IF EXISTS audit_upload; DROP TRIGGER IF EXISTS audit_delete; DROP TRIGGER IF EXISTS audit_pending;");

                if (mode == "upload-intent")
                {
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER audit_pending BEFORE INSERT ON PendingUploadAudits BEGIN SELECT RAISE(FAIL,'Audit initial journal unavailable'); END;");
                }

                if (mode == "deadline")
                {
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER audit_todo BEFORE UPDATE ON TodoTaskMetadata BEGIN SELECT RAISE(FAIL,'Audit local deadline write unavailable'); END;");
                }

                if (mode == "upload")
                {
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER audit_upload BEFORE INSERT ON UploadAuditLogs BEGIN SELECT RAISE(FAIL,'Audit upload record unavailable'); END;");
                }

                if (mode == "delete-audit")
                {
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER audit_delete BEFORE UPDATE ON CalendarEventDeletionAuditLogs WHEN NEW.Status='Succeeded' BEGIN SELECT RAISE(FAIL,'Audit deletion status unavailable'); END;");
                }
            }
            return Results.Ok(new
            {
                mode
            });
        });

        app.MapPost("/fixture/recover", async (IServiceProvider provider) =>
        {
            Type recovery = typeof(App).Assembly.GetType("OrganizationHub.Web.Services.AuditRecoveryService")!;
            await (Task)recovery.GetMethod("RecoverAsync")!.Invoke(provider.GetRequiredService(recovery), new object[] { CancellationToken.None })!;
            await provider.GetRequiredService<GoogleTasksTodoService>().RecoverDeadlineChangesAsync(CancellationToken.None);
            return Results.Ok();
        });
        app.Run();


    }
}
