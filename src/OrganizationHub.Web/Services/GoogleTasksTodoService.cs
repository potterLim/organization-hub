using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Data;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService : ITodoService
{
    private const string TASK_STATUS_COMPLETED = "completed";
    private const string TASK_STATUS_NEEDS_ACTION = "needsAction";
    private const string TASK_FIELDS = "id,title,status,position,updated,completed";
    private static readonly string[] TASK_SCOPES = new[] { GoogleTasksApi.Scope.Tasks };

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly ILogger<GoogleTasksTodoService> mLogger;
    private readonly Configuration.GoogleApiOptions mOptions;
    private readonly IChangeVersionService mChangeVersionService;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public GoogleTasksTodoService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IGoogleApiCredentialFactory credentialFactory,
        ILogger<GoogleTasksTodoService> logger,
        IOptions<Configuration.GoogleApiOptions> options,
        IChangeVersionService changeVersionService)
        : this(dbContextFactory, credentialFactory, logger, options, changeVersionService, new OrganizationPermissionService(dbContextFactory))
    {
    }

    public GoogleTasksTodoService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IGoogleApiCredentialFactory credentialFactory,
        ILogger<GoogleTasksTodoService> logger,
        IOptions<Configuration.GoogleApiOptions> options,
        IChangeVersionService changeVersionService,
        IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mCredentialFactory = credentialFactory;
        mLogger = logger;
        mOptions = options.Value;
        mChangeVersionService = changeVersionService;
        mOrganizationPermissionService = organizationPermissionService;
    }

}
