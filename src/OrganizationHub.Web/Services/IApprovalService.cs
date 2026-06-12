using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IApprovalService
{
    Task<ApprovalCreateContext> GetCreateContextAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> SubmitRequestAsync(UserSession userSession, ApprovalRequestFormModel formModel, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetPendingApprovalsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetRelatedHistoryAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequestItem>> GetApprovedRequestsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<ServiceResult> ApproveRequestAsync(UserSession userSession, int approvalRequestID, int nextApproverUserID, string comment, CancellationToken cancellationToken);
    Task<ServiceResult> RejectRequestAsync(UserSession userSession, int approvalRequestID, string comment, CancellationToken cancellationToken);
}
