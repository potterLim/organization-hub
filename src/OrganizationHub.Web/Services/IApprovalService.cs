using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IApprovalService
{
    Task<ApprovalCreateContext> GetCreateContextAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> SubmitRequestAsync(UserSession userSession, ApprovalRequestCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetPendingApprovalsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetRelatedHistoryAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetApprovedRequestsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> ApproveRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        ApprovalUserId nextApproverUserId,
        string comment,
        CancellationToken cancellationToken);

    Task<ServiceResult> RejectRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        string comment,
        CancellationToken cancellationToken);
}
