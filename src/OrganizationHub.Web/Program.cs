using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        configureServices(builder);
        WebApplication app = builder.Build();
        seedDatabase(app);
        configurePipeline(app);
        app.Run();
    }

    private static void configureServices(WebApplicationBuilder builder)
    {
        string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDirectory);

        builder.Services.Configure<OrganizationHubOptions>(builder.Configuration.GetSection("OrganizationHub"));
        builder.Services.Configure<GoogleApiOptions>(builder.Configuration.GetSection("GoogleApi"));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
        });

        string? connectionStringOrNull = builder.Configuration.GetConnectionString("OrganizationHub");
        if (string.IsNullOrWhiteSpace(connectionStringOrNull))
        {
            throw new InvalidOperationException("OrganizationHub connection string is required.");
        }

        string dataProtectionDirectory = Path.Combine(dataDirectory, "DataProtectionKeys");
        Directory.CreateDirectory(dataProtectionDirectory);
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory)).SetApplicationName("OrganizationHub");
        builder.Services.AddDbContextFactory<OrganizationHubDbContext>(options => options.UseSqlite(connectionStringOrNull));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddMudServices();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IChangeVersionService, ChangeVersionService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "OrganizationHub.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/access-denied";
            options.SlidingExpiration = true;
            options.Events = new CookieAuthenticationEvents();
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.Redirect("/login");
                return System.Threading.Tasks.Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect("/access-denied");
                return System.Threading.Tasks.Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization(options =>
        {
            addPermissionPolicy(options, OrganizationHubPolicies.MANAGEMENT_ACCESS, createPermissionKeys(PermissionKeys.FILES_READ), getManagementFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.LEADER_ACCESS, createPermissionKeys(PermissionKeys.FILES_READ), getLeaderFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.SYSTEM_SETTINGS_MANAGE, createPermissionKeys(PermissionKeys.SYSTEM_SETTINGS_MANAGE), getAdministratorFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.ORGANIZATION_MANAGE, createPermissionKeys(PermissionKeys.ORGANIZATION_MANAGE), getAdministratorFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.POSITIONS_MANAGE, createPermissionKeys(PermissionKeys.POSITIONS_MANAGE, PermissionKeys.PERMISSIONS_MANAGE), getAdministratorFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.USERS_MANAGE, createPermissionKeys(PermissionKeys.USERS_MANAGE), getManagementManagerFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.GOOGLE_CONNECTION_MANAGE, createPermissionKeys(PermissionKeys.GOOGLE_CONNECTION_MANAGE), getAdministratorFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.MENUS_MANAGE, createPermissionKeys(PermissionKeys.MENUS_MANAGE), getAdministratorFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.CALENDAR_AUDIT_READ, createPermissionKeys(PermissionKeys.CALENDAR_AUDIT_READ), getManagementManagerFallbackRoles());
            addPermissionPolicy(options, OrganizationHubPolicies.APPROVAL_WORKFLOWS_MANAGE, createPermissionKeys(PermissionKeys.APPROVAL_WORKFLOWS_MANAGE), getAdministratorFallbackRoles());
        });

        builder.Services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserManagementService, UserManagementService>();
        builder.Services.AddScoped<ITeamManagementService, TeamManagementService>();
        builder.Services.AddScoped<IPositionManagementService, PositionManagementService>();
        builder.Services.AddScoped<IOrganizationPermissionService, OrganizationPermissionService>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddScoped<IGoogleApiCredentialFactory, GoogleApiCredentialFactory>();
        builder.Services.AddScoped<IGoogleConnectionService, GoogleConnectionService>();
        builder.Services.AddScoped<IGoogleHealthCheckService, GoogleHealthCheckService>();
        builder.Services.AddScoped<IGoogleWorkspaceBrowserService, GoogleWorkspaceBrowserService>();
        builder.Services.AddScoped<IGoogleWorkspaceSetupService, GoogleWorkspaceSetupService>();
        builder.Services.AddScoped<IDriveService, GoogleDriveService>();
        builder.Services.AddScoped<ICalendarService, GoogleCalendarService>();
        builder.Services.AddScoped<ITodoService, GoogleTasksTodoService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<IFilePolicyService, FilePolicyService>();
        builder.Services.AddScoped<IDriveBrowserService, DriveBrowserService>();
        builder.Services.AddScoped<INavigationMenuService, NavigationMenuService>();
        builder.Services.AddScoped<IUploadAuditLogService, UploadAuditLogService>();
        builder.Services.AddScoped<ICalendarEventDeletionAuditLogService, CalendarEventDeletionAuditLogService>();
        builder.Services.AddScoped<IApprovalService, ApprovalService>();
        builder.Services.AddScoped<IApprovalWorkflowManagementService, ApprovalWorkflowManagementService>();
        builder.Services.AddScoped<OrganizationHubDataSeeder>();
    }

    private static void addPermissionPolicy(AuthorizationOptions options, string policyName, IReadOnlyList<PermissionKey> permissionKeys, IReadOnlyList<string> fallbackRoleNames)
    {
        options.AddPolicy(policyName, policy => policy.Requirements.Add(new PermissionAuthorizationRequirement(permissionKeys, fallbackRoleNames)));
    }

    private static IReadOnlyList<PermissionKey> createPermissionKeys(params string[] permissionKeyValues)
    {
        List<PermissionKey> permissionKeys = new List<PermissionKey>();
        foreach (string permissionKeyValue in permissionKeyValues)
        {
            permissionKeys.Add(new PermissionKey(permissionKeyValue));
        }

        return permissionKeys;
    }

    private static IReadOnlyList<string> getAdministratorFallbackRoles()
    {
        return new[] { OrganizationHubRoles.ADMIN };
    }

    private static IReadOnlyList<string> getManagementManagerFallbackRoles()
    {
        return new[]
        {
            OrganizationHubRoles.ADMIN,
            OrganizationHubRoles.MANAGEMENT_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_DEPUTY_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_MANAGER
        };
    }

    private static IReadOnlyList<string> getManagementFallbackRoles()
    {
        return new[]
        {
            OrganizationHubRoles.ADMIN,
            OrganizationHubRoles.MANAGEMENT_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_DEPUTY_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_MANAGER,
            OrganizationHubRoles.MANAGEMENT_STAFF,
            OrganizationHubRoles.MANAGEMENT_RECORDER
        };
    }

    private static IReadOnlyList<string> getLeaderFallbackRoles()
    {
        return new[]
        {
            OrganizationHubRoles.ADMIN,
            OrganizationHubRoles.MANAGEMENT_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_DEPUTY_DIRECTOR,
            OrganizationHubRoles.MANAGEMENT_MANAGER,
            OrganizationHubRoles.MANAGEMENT_STAFF,
            OrganizationHubRoles.MANAGEMENT_RECORDER,
            OrganizationHubRoles.TEAM_LEADER
        };
    }

    private static void configurePipeline(WebApplication app)
    {
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment() == false)
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapGet("/healthz", () => Results.Ok("ok"));
        app.MapControllers();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    }

    private static void seedDatabase(WebApplication app)
    {
        using (IServiceScope serviceScope = app.Services.CreateScope())
        {
            OrganizationHubDataSeeder dataSeeder = serviceScope.ServiceProvider.GetRequiredService<OrganizationHubDataSeeder>();
            dataSeeder.SeedAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
