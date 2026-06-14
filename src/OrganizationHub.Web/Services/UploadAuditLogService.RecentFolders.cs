using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForFolderAsync(string folderID, TimeSpan maximumAge, CancellationToken cancellationToken)
    {
        DriveFolderID requestedFolderID = new DriveFolderID(folderID);
        if (requestedFolderID.HasValue == false)
        {
            return new List<UploadAuditLogItem>();
        }

        DateTime cutoff = DateTime.UtcNow.Subtract(maximumAge);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => auditLog.FolderID == requestedFolderID.Value && auditLog.UploadedAt >= cutoff).OrderByDescending(auditLog => auditLog.UploadedAt).Take(100).ToListAsync(cancellationToken);
            return createItems(auditLogs);
        }
    }
}
