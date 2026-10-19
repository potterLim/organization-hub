using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IApprovalPublicationService
{
    Task<ServiceResult> PublishApprovedFileAsync(ApprovalRequestId requestId, CancellationToken cancellationToken);
}
