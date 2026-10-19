using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IApprovalPublicationService
{
    Task<ApprovalPublicationPage> GetPendingPublicationsAsync(UserSession userSession, int pageNumber, CancellationToken cancellationToken);

    Task<ServiceResult> RetryPublicationAsAdministratorAsync(UserSession userSession, ApprovalRequestId requestId, CancellationToken cancellationToken);

    Task<ServiceResult> PublishApprovedFileAsync(ApprovalRequestId requestId, CancellationToken cancellationToken);
}
