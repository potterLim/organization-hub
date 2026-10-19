using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDbContext : DbContext
{
    public DbSet<TodoDeadlineChange> TodoDeadlineChanges
    {
        get
        {
            return Set<TodoDeadlineChange>();
        }
    }
    public DbSet<RevokedLoginSession> RevokedLoginSessions
    {
        get
        {
            return Set<RevokedLoginSession>();
        }
    }
    public DbSet<ApprovalDocument> ApprovalDocuments
    {
        get; set;
    }
    public DbSet<ApprovalPublication> ApprovalPublications
    {
        get; set;
    }
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
    public DbSet<NavigationMenuSection> NavigationMenuSections
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
    public DbSet<WorkGuidelineSection> WorkGuidelineSections
    {
        get; set;
    }

    public OrganizationHubDbContext(DbContextOptions<OrganizationHubDbContext> options) : base(options)
    {
        ApprovalDocuments = Set<ApprovalDocument>();
        ApprovalPublications = Set<ApprovalPublication>();
        UserAccounts = Set<UserAccount>();
        Teams = Set<Team>();
        Positions = Set<OrganizationPosition>();
        PositionPermissionGrants = Set<PositionPermissionGrant>();
        ApprovalWorkflows = Set<ApprovalWorkflow>();
        ApprovalWorkflowSteps = Set<ApprovalWorkflowStep>();
        GlobalSettings = Set<GlobalSetting>();
        GoogleStorageConnections = Set<GoogleStorageConnection>();
        NavigationMenuItems = Set<NavigationMenuItem>();
        NavigationMenuSections = Set<NavigationMenuSection>();
        NavigationMenuTeamPermissions = Set<NavigationMenuTeamPermission>();
        UploadAuditLogs = Set<UploadAuditLog>();
        CalendarEventDeletionAuditLogs = Set<CalendarEventDeletionAuditLog>();
        ApprovalRequests = Set<ApprovalRequest>();
        ApprovalSteps = Set<ApprovalStep>();
        ApprovalAuditLogs = Set<ApprovalAuditLog>();
        TodoTaskMetadata = Set<TodoTaskMetadata>();
        CalendarEventMetadata = Set<CalendarEventMetadata>();
        WorkGuidelineSections = Set<WorkGuidelineSection>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        prepareSettingRevisions();
        prepareAdministrativeRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        prepareSettingRevisions();
        prepareAdministrativeRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevokedLoginSession>().HasKey(session => session.SessionId);
        modelBuilder.Entity<RevokedLoginSession>().Property(session => session.SessionId).HasMaxLength(32);
        modelBuilder.Entity<RevokedLoginSession>().HasOne<UserAccount>().WithMany().HasForeignKey(session => session.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WorkGuidelineState>().Property(state => state.Revision).IsConcurrencyToken();
        modelBuilder.Entity<WorkGuidelineState>().HasData(new WorkGuidelineState { Id = 1, Revision = 0 });
        modelBuilder.applyOrganizationHubModelConfiguration();
    }

    private void prepareSettingRevisions()
    {
        ChangeTracker.DetectChanges();
        foreach (EntityEntry<GlobalSetting> entry in ChangeTracker.Entries<GlobalSetting>())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            entry.Entity.Revision = entry.Property(setting => setting.Revision).OriginalValue + 1;
            if (entry.Property(setting => setting.ShouldShowTodoAuthor).IsModified || entry.Property(setting => setting.ShouldShowCalendarAuthor).IsModified)
            {
                entry.Entity.DisplayOptionsRevision = entry.Property(setting => setting.DisplayOptionsRevision).OriginalValue + 1;
            }

            if (entry.Property(setting => setting.NoticeFileId).IsModified || entry.Property(setting => setting.WorkGuidelineFileId).IsModified)
            {
                entry.Entity.FileResourcesRevision = entry.Property(setting => setting.FileResourcesRevision).OriginalValue + 1;
            }
        }
    }
}
