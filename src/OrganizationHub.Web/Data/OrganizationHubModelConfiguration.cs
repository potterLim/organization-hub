using Microsoft.EntityFrameworkCore;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    internal static void applyOrganizationHubModelConfiguration(this ModelBuilder modelBuilder)
    {
        configureUsers(modelBuilder);
        configureOrganizationStructure(modelBuilder);
        configureApprovalWorkflows(modelBuilder);
        configureWorkspaceSettings(modelBuilder);
        configureNavigation(modelBuilder);
        configureAuditLogs(modelBuilder);
        configureApprovals(modelBuilder);
        configureMetadata(modelBuilder);
        configureWorkGuidelines(modelBuilder);
    }
}
