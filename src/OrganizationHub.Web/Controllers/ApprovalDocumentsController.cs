using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Controllers;

[Authorize]
public sealed class ApprovalDocumentsController : Controller
{
    private readonly IApprovalService mApprovalService;

    public ApprovalDocumentsController(IApprovalService approvalService)
    {
        mApprovalService = approvalService;
    }

    [HttpGet("/approvals/{requestId:int:min(1)}/document")]
    public async Task<IActionResult> DownloadAsync(int requestId)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(User);
        if (userSessionOrNull == null)
        {
            return Unauthorized();
        }

        FileDownloadResult? documentOrNull = await mApprovalService.GetDocumentSnapshotOrNullAsync(userSessionOrNull, ApprovalRequestId.CreateExisting(requestId), HttpContext.RequestAborted);
        if (documentOrNull == null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(documentOrNull.ContentStream, documentOrNull.ContentType, documentOrNull.FileName);
    }
}
