using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ApprovalPreflightRegressionTests
{
    private sealed class FailedConflictService : IDriveFileNameConflictService
    {
        public Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(DriveFolderId folderId, DriveItemName fileName, CancellationToken cancellationToken)
        {
            throw new IOException("Drive unavailable");
        }
    }

    [Fact]
    public async Task DriveFailureBeforeApprovalKeepsCircuitAndCommentAsync()
    {
        ApprovalInbox page = new ApprovalInbox();
        typeof(ApprovalInbox).GetProperty("Logger", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, Microsoft.Extensions.Logging.Abstractions.NullLogger<ApprovalInbox>.Instance);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", flags)!.SetValue(page, OperationPermissionRegressionTests.createSession());
        typeof(ApprovalInbox).GetProperty("DriveFileNameConflictService", flags)!.SetValue(page, new FailedConflictService());
        typeof(ApprovalInbox).GetMethod("setCommentValue", flags)!.Invoke(page, new object[] { ApprovalRequestId.CreateExisting(1), "검토 의견 유지" });
        ApprovalRequestItem request = new ApprovalRequestItem();
        request.Id = 1;
        request.StepCount = 1;
        request.CurrentStepNumber = 1;
        request.ApprovedFolderId = "approved";
        request.FileName = "document.pdf";
        MethodInfo approve = typeof(ApprovalInbox).GetMethod("approveRequestAsync", flags, null, new Type[] { typeof(ApprovalRequestItem) }, null)!;
        await (Task)approve.Invoke(page, new object[] { request })!;
        Assert.Contains("다시", (string)typeof(ApprovalInbox).GetField("mMessage", flags)!.GetValue(page)!);
        Assert.Equal("검토 의견 유지", typeof(ApprovalInbox).GetMethod("getCommentValue", flags)!.Invoke(page, new object[] { ApprovalRequestId.CreateExisting(1) }));
    }
}
