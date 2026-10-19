using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IWorkGuidelineService
{
    Task<WorkGuidelineSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
    Task<ServiceResult> SaveSectionsAsync(IReadOnlyList<WorkGuidelineSectionFormModel> formModels, int expectedRevision, CancellationToken cancellationToken);
}
