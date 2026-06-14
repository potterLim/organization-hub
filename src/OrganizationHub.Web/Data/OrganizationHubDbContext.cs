using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed class OrganizationHubDbContext : DbContext
{
    public DbSet<UserAccount> UserAccounts
    {
        get; set;
    }
    public DbSet<Team> Teams
    {
        get; set;
    }
    public DbSet<OrganizationPosition> Positions
    {
        get; set;
    }
    public DbSet<PositionPermissionGrant> PositionPermissionGrants
    {
        get; set;
    }
    public DbSet<ApprovalWorkflow> ApprovalWorkflows
    {
        get; set;
    }
    public DbSet<ApprovalWorkflowStep> ApprovalWorkflowSteps
    {
        get; set;
    }
    public DbSet<GlobalSetting> GlobalSettings
    {
        get; set;
    }
    public DbSet<GoogleStorageConnection> GoogleStorageConnections
    {
        get; set;
    }
    public DbSet<NavigationMenuItem> NavigationMenuItems
    {
        get; set;
    }
    public DbSet<NavigationMenuTeamPermission> NavigationMenuTeamPermissions
    {
        get; set;
    }
    public DbSet<UploadAuditLog> UploadAuditLogs
    {
        get; set;
    }
    public DbSet<CalendarEventDeletionAuditLog> CalendarEventDeletionAuditLogs
    {
        get; set;
    }
    public DbSet<ApprovalRequest> ApprovalRequests
    {
        get; set;
    }
    public DbSet<ApprovalStep> ApprovalSteps
    {
        get; set;
    }
    public DbSet<ApprovalAuditLog> ApprovalAuditLogs
    {
        get; set;
    }
    public DbSet<TodoTaskMetadata> TodoTaskMetadata
    {
        get; set;
    }
    public DbSet<CalendarEventMetadata> CalendarEventMetadata
    {
        get; set;
    }

    public OrganizationHubDbContext(DbContextOptions<OrganizationHubDbContext> options) : base(options)
    {
        UserAccounts = Set<UserAccount>();
        Teams = Set<Team>();
        Positions = Set<OrganizationPosition>();
        PositionPermissionGrants = Set<PositionPermissionGrant>();
        ApprovalWorkflows = Set<ApprovalWorkflow>();
        ApprovalWorkflowSteps = Set<ApprovalWorkflowStep>();
        GlobalSettings = Set<GlobalSetting>();
        GoogleStorageConnections = Set<GoogleStorageConnection>();
        NavigationMenuItems = Set<NavigationMenuItem>();
        NavigationMenuTeamPermissions = Set<NavigationMenuTeamPermission>();
        UploadAuditLogs = Set<UploadAuditLog>();
        CalendarEventDeletionAuditLogs = Set<CalendarEventDeletionAuditLog>();
        ApprovalRequests = Set<ApprovalRequest>();
        ApprovalSteps = Set<ApprovalStep>();
        ApprovalAuditLogs = Set<ApprovalAuditLog>();
        TodoTaskMetadata = Set<TodoTaskMetadata>();
        CalendarEventMetadata = Set<CalendarEventMetadata>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.applyOrganizationHubModelConfiguration();
    }
}
