using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
        EntityTypeBuilder<UserAccount> userBuilder = modelBuilder.Entity<UserAccount>();
        userBuilder.HasKey(user => user.ID);
        userBuilder.HasIndex(user => user.Username).IsUnique();
        userBuilder.Property(user => user.Username).HasMaxLength(64).IsRequired();
        userBuilder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
        userBuilder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        userBuilder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        userBuilder.HasOne(user => user.Team).WithMany(team => team.UserAccounts).HasForeignKey(user => user.TeamID).OnDelete(DeleteBehavior.Restrict);
        userBuilder.HasOne(user => user.Position).WithMany(position => position.UserAccounts).HasForeignKey(user => user.PositionID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<Team> teamBuilder = modelBuilder.Entity<Team>();
        teamBuilder.HasKey(team => team.ID);
        teamBuilder.HasIndex(team => team.TeamKey).IsUnique();
        teamBuilder.Property(team => team.TeamKey).HasMaxLength(64).IsRequired();
        teamBuilder.Property(team => team.DisplayName).HasMaxLength(120).IsRequired();
        teamBuilder.Property(team => team.MaterialsFolderID).HasMaxLength(256);
        teamBuilder.Property(team => team.ApprovedFolderID).HasMaxLength(256);
        teamBuilder.Property(team => team.CalendarID).HasMaxLength(256);
        teamBuilder.Property(team => team.TaskListID).HasMaxLength(256);

        EntityTypeBuilder<OrganizationPosition> positionBuilder = modelBuilder.Entity<OrganizationPosition>();
        positionBuilder.HasKey(position => position.ID);
        positionBuilder.HasIndex(position => new { position.TeamID, position.Name }).IsUnique();
        positionBuilder.Property(position => position.Name).HasMaxLength(120).IsRequired();
        positionBuilder.Property(position => position.Description).HasMaxLength(500).IsRequired();
        positionBuilder.HasOne(position => position.Team).WithMany(team => team.Positions).HasForeignKey(position => position.TeamID).OnDelete(DeleteBehavior.Cascade);

        EntityTypeBuilder<PositionPermissionGrant> permissionGrantBuilder = modelBuilder.Entity<PositionPermissionGrant>();
        permissionGrantBuilder.HasKey(permissionGrant => permissionGrant.ID);
        permissionGrantBuilder.HasIndex(permissionGrant => new { permissionGrant.PositionID, permissionGrant.PermissionKey, permissionGrant.ScopeType, permissionGrant.TargetTeamID }).IsUnique();
        permissionGrantBuilder.Property(permissionGrant => permissionGrant.PermissionKey).HasMaxLength(120).IsRequired();
        permissionGrantBuilder.Property(permissionGrant => permissionGrant.ScopeType).HasConversion<string>().HasMaxLength(32).IsRequired();
        permissionGrantBuilder.HasOne(permissionGrant => permissionGrant.Position).WithMany(position => position.PermissionGrants).HasForeignKey(permissionGrant => permissionGrant.PositionID).OnDelete(DeleteBehavior.Cascade);
        permissionGrantBuilder.HasOne(permissionGrant => permissionGrant.TargetTeam).WithMany().HasForeignKey(permissionGrant => permissionGrant.TargetTeamID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<ApprovalWorkflow> approvalWorkflowBuilder = modelBuilder.Entity<ApprovalWorkflow>();
        approvalWorkflowBuilder.HasKey(approvalWorkflow => approvalWorkflow.ID);
        approvalWorkflowBuilder.HasIndex(approvalWorkflow => new { approvalWorkflow.AppliesToTeamID, approvalWorkflow.Priority });
        approvalWorkflowBuilder.Property(approvalWorkflow => approvalWorkflow.Name).HasMaxLength(120).IsRequired();
        approvalWorkflowBuilder.Property(approvalWorkflow => approvalWorkflow.Description).HasMaxLength(500).IsRequired();
        approvalWorkflowBuilder.HasOne(approvalWorkflow => approvalWorkflow.AppliesToTeam).WithMany(team => team.ApprovalWorkflows).HasForeignKey(approvalWorkflow => approvalWorkflow.AppliesToTeamID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<ApprovalWorkflowStep> approvalWorkflowStepBuilder = modelBuilder.Entity<ApprovalWorkflowStep>();
        approvalWorkflowStepBuilder.HasKey(approvalWorkflowStep => approvalWorkflowStep.ID);
        approvalWorkflowStepBuilder.HasIndex(approvalWorkflowStep => new { approvalWorkflowStep.ApprovalWorkflowID, approvalWorkflowStep.StepOrder }).IsUnique();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.StepName).HasMaxLength(120).IsRequired();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.ApproverSelectionType).HasConversion<string>().HasMaxLength(48).IsRequired();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.RequiredPermissionKey).HasMaxLength(120).IsRequired();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.ScopeType).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalWorkflowStepBuilder.HasOne(approvalWorkflowStep => approvalWorkflowStep.ApprovalWorkflow).WithMany(approvalWorkflow => approvalWorkflow.Steps).HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApprovalWorkflowID).OnDelete(DeleteBehavior.Cascade);
        approvalWorkflowStepBuilder.HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverUser).WithMany().HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverUserID).OnDelete(DeleteBehavior.Restrict);
        approvalWorkflowStepBuilder.HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverPosition).WithMany(position => position.ApprovalWorkflowSteps).HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverPositionID).OnDelete(DeleteBehavior.Restrict);
        approvalWorkflowStepBuilder.HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverTeam).WithMany().HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverTeamID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<GlobalSetting> settingBuilder = modelBuilder.Entity<GlobalSetting>();
        settingBuilder.HasKey(setting => setting.ID);
        settingBuilder.Property(setting => setting.OfficialRootFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.PublicFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.CommonFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleResourcesFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.WorkResourcesFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ArchiveFolderID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.OrganizationCalendarID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleCalendarID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.OrganizationTaskListID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleTaskListID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.NoticeFileID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.WorkGuidelineFileID).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ShowTodoAuthor).HasDefaultValue(true);
        settingBuilder.Property(setting => setting.ShowCalendarAuthor).HasDefaultValue(true);

        EntityTypeBuilder<GoogleStorageConnection> googleConnectionBuilder = modelBuilder.Entity<GoogleStorageConnection>();
        googleConnectionBuilder.HasKey(connection => connection.ID);
        googleConnectionBuilder.Property(connection => connection.ConnectedEmail).HasMaxLength(320).IsRequired();
        googleConnectionBuilder.Property(connection => connection.EncryptedRefreshToken).IsRequired();
        googleConnectionBuilder.Property(connection => connection.Scopes).HasMaxLength(1024).IsRequired();

        EntityTypeBuilder<NavigationMenuItem> menuBuilder = modelBuilder.Entity<NavigationMenuItem>();
        menuBuilder.HasKey(menuItem => menuItem.ID);
        menuBuilder.HasIndex(menuItem => menuItem.MenuKey).IsUnique();
        menuBuilder.Property(menuItem => menuItem.MenuKey).HasMaxLength(64).IsRequired();
        menuBuilder.Property(menuItem => menuItem.DisplayName).HasMaxLength(80).IsRequired();
        menuBuilder.Property(menuItem => menuItem.Href).HasMaxLength(256).IsRequired();
        menuBuilder.Property(menuItem => menuItem.IconKey).HasMaxLength(64).IsRequired();
        menuBuilder.Property(menuItem => menuItem.AccessLevel).HasConversion<string>().HasMaxLength(32).IsRequired();

        EntityTypeBuilder<NavigationMenuTeamPermission> menuPermissionBuilder = modelBuilder.Entity<NavigationMenuTeamPermission>();
        menuPermissionBuilder.HasKey(menuPermission => new { menuPermission.NavigationMenuItemID, menuPermission.TeamID });
        menuPermissionBuilder.HasOne(menuPermission => menuPermission.NavigationMenuItem).WithMany(menuItem => menuItem.TeamPermissions).HasForeignKey(menuPermission => menuPermission.NavigationMenuItemID).OnDelete(DeleteBehavior.Cascade);
        menuPermissionBuilder.HasOne(menuPermission => menuPermission.Team).WithMany(team => team.NavigationMenuPermissions).HasForeignKey(menuPermission => menuPermission.TeamID).OnDelete(DeleteBehavior.Cascade);

        EntityTypeBuilder<UploadAuditLog> uploadAuditBuilder = modelBuilder.Entity<UploadAuditLog>();
        uploadAuditBuilder.HasKey(auditLog => auditLog.ID);
        uploadAuditBuilder.HasIndex(auditLog => auditLog.FileID);
        uploadAuditBuilder.HasIndex(auditLog => new { auditLog.FileID, auditLog.UploadedAt });
        uploadAuditBuilder.HasIndex(auditLog => auditLog.FolderID);
        uploadAuditBuilder.HasIndex(auditLog => new { auditLog.FolderID, auditLog.UploadedAt });
        uploadAuditBuilder.Property(auditLog => auditLog.FileID).HasMaxLength(256).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FileName).HasMaxLength(512).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderID).HasMaxLength(256).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderDisplayName).HasMaxLength(160).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.UploaderDisplayName).HasMaxLength(120).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.UploaderRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.TeamName).HasMaxLength(120).IsRequired();

        EntityTypeBuilder<CalendarEventDeletionAuditLog> calendarDeletionAuditBuilder = modelBuilder.Entity<CalendarEventDeletionAuditLog>();
        calendarDeletionAuditBuilder.HasKey(auditLog => auditLog.ID);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.CalendarID);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.EventID);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.DeletedAt);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.DeletedByUserID);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.Status);
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.CalendarID).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.CalendarName).HasMaxLength(160).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventID).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventSummary).HasMaxLength(512).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventLocation).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByDisplayName).HasMaxLength(120).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByTeamName).HasMaxLength(120).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.ErrorMessage).HasMaxLength(1024).IsRequired();

        EntityTypeBuilder<ApprovalRequest> approvalRequestBuilder = modelBuilder.Entity<ApprovalRequest>();
        approvalRequestBuilder.HasKey(request => request.ID);
        approvalRequestBuilder.HasIndex(request => request.FileID);
        approvalRequestBuilder.HasIndex(request => request.ApprovedFileID);
        approvalRequestBuilder.HasIndex(request => request.TeamID);
        approvalRequestBuilder.HasIndex(request => request.RequesterUserID);
        approvalRequestBuilder.HasIndex(request => request.Status);
        approvalRequestBuilder.Property(request => request.Title).HasMaxLength(180).IsRequired();
        approvalRequestBuilder.Property(request => request.FileID).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.FileName).HasMaxLength(512).IsRequired();
        approvalRequestBuilder.Property(request => request.MimeType).HasMaxLength(160).IsRequired();
        approvalRequestBuilder.Property(request => request.SourceFolderID).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.SourceFolderKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalRequestBuilder.Property(request => request.TeamName).HasMaxLength(120).IsRequired();
        approvalRequestBuilder.Property(request => request.RequesterDisplayName).HasMaxLength(120).IsRequired();
        approvalRequestBuilder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFileID).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFolderID).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFolderDisplayName).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.HasOne(request => request.Team).WithMany(team => team.ApprovalRequests).HasForeignKey(request => request.TeamID).OnDelete(DeleteBehavior.Restrict);
        approvalRequestBuilder.HasOne(request => request.RequesterUser).WithMany().HasForeignKey(request => request.RequesterUserID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<ApprovalStep> approvalStepBuilder = modelBuilder.Entity<ApprovalStep>();
        approvalStepBuilder.HasKey(step => step.ID);
        approvalStepBuilder.HasIndex(step => new { step.ApprovalRequestID, step.StepNumber }).IsUnique();
        approvalStepBuilder.HasIndex(step => step.ApproverUserID);
        approvalStepBuilder.HasIndex(step => step.Status);
        approvalStepBuilder.Property(step => step.ApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalStepBuilder.Property(step => step.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalStepBuilder.Property(step => step.Comment).HasMaxLength(1000).IsRequired();
        approvalStepBuilder.HasOne(step => step.ApprovalRequest).WithMany(request => request.Steps).HasForeignKey(step => step.ApprovalRequestID).OnDelete(DeleteBehavior.Cascade);
        approvalStepBuilder.HasOne(step => step.ApproverUser).WithMany().HasForeignKey(step => step.ApproverUserID).OnDelete(DeleteBehavior.Restrict);

        EntityTypeBuilder<ApprovalAuditLog> approvalAuditBuilder = modelBuilder.Entity<ApprovalAuditLog>();
        approvalAuditBuilder.HasKey(auditLog => auditLog.ID);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.ApprovalRequestID);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.ActorUserID);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.Action);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.CreatedAt);
        approvalAuditBuilder.Property(auditLog => auditLog.ActorDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.Comment).HasMaxLength(1000).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.PreviousApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.NewApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder.HasOne(auditLog => auditLog.ApprovalRequest).WithMany(request => request.AuditLogs).HasForeignKey(auditLog => auditLog.ApprovalRequestID).OnDelete(DeleteBehavior.Cascade);

        EntityTypeBuilder<TodoTaskMetadata> todoTaskMetadataBuilder = modelBuilder.Entity<TodoTaskMetadata>();
        todoTaskMetadataBuilder.HasKey(metadata => metadata.ID);
        todoTaskMetadataBuilder.HasIndex(metadata => new { metadata.TaskListID, metadata.TaskID }).IsUnique();
        todoTaskMetadataBuilder.HasIndex(metadata => metadata.CreatedByUserID);
        todoTaskMetadataBuilder.Property(metadata => metadata.TaskListID).HasMaxLength(256).IsRequired();
        todoTaskMetadataBuilder.Property(metadata => metadata.TaskID).HasMaxLength(256).IsRequired();
        todoTaskMetadataBuilder.Property(metadata => metadata.CreatedByDisplayName).HasMaxLength(120).IsRequired();

        EntityTypeBuilder<CalendarEventMetadata> calendarEventMetadataBuilder = modelBuilder.Entity<CalendarEventMetadata>();
        calendarEventMetadataBuilder.HasKey(metadata => metadata.ID);
        calendarEventMetadataBuilder.HasIndex(metadata => new { metadata.CalendarID, metadata.EventID }).IsUnique();
        calendarEventMetadataBuilder.HasIndex(metadata => metadata.CreatedByUserID);
        calendarEventMetadataBuilder.Property(metadata => metadata.CalendarID).HasMaxLength(256).IsRequired();
        calendarEventMetadataBuilder.Property(metadata => metadata.EventID).HasMaxLength(256).IsRequired();
        calendarEventMetadataBuilder.Property(metadata => metadata.CreatedByDisplayName).HasMaxLength(120).IsRequired();
    }
}
