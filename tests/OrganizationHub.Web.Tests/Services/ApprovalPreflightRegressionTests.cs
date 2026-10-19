using System;
using System.Collections.Generic;
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
        public bool ShouldFail { get; set; } = true;

        public Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(DriveFolderId folderId, DriveItemName fileName, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new IOException("Drive unavailable");
            }

            return Task.FromResult(new DriveFileNameConflictCheckResult(fileName, fileName, Array.Empty<DriveFileItem>()));
        }
    }

    public class ApprovalSpy : DispatchProxy
    {
        public int ApprovalCalls
        {
            get; private set;
        }

        protected override object? Invoke(MethodInfo? targetMethodOrNull, object?[]? argsOrNull)
        {
            if (targetMethodOrNull?.Name == nameof(IApprovalService.ApproveRequestAsync))
            {
                ApprovalCalls++;
                return Task.FromResult(ServiceResult.Succeed());
            }

            if (targetMethodOrNull?.Name == nameof(IApprovalService.GetPendingApprovalsAsync))
            {
                return Task.FromResult<IReadOnlyList<ApprovalRequestItem>>(Array.Empty<ApprovalRequestItem>());
            }

            throw new InvalidOperationException("Unexpected approval operation.");
        }
    }

    [Fact]
    public async Task DriveFailureBeforeApprovalKeepsCircuitAndCommentAsync()
    {
        ApprovalInbox page = new ApprovalInbox();
        typeof(ApprovalInbox).GetProperty("Logger", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, Microsoft.Extensions.Logging.Abstractions.NullLogger<ApprovalInbox>.Instance);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", flags)!.SetValue(page, OperationPermissionRegressionTests.createSession());
        FailedConflictService conflicts = new FailedConflictService();
        IApprovalService approvalService = DispatchProxy.Create<IApprovalService, ApprovalSpy>();
        typeof(ApprovalInbox).GetProperty("DriveFileNameConflictService", flags)!.SetValue(page, conflicts);
        typeof(ApprovalInbox).GetProperty("ApprovalService", flags)!.SetValue(page, approvalService);
        typeof(ApprovalInbox).GetMethod("setCommentValue", flags)!.Invoke(page, new object[] { ApprovalRequestId.CreateExisting(1), "검토 의견 유지" });
        ApprovalRequestItem request = new ApprovalRequestItem();
        request.Id = 1;
        request.StepCount = 1;
        request.CurrentStepNumber = 1;
        request.ApprovedFolderId = "approved";
        request.FileName = "document.pdf";
        MethodInfo approve = typeof(ApprovalInbox).GetMethod("approveRequestAsync", flags, null, new Type[] { typeof(ApprovalRequestItem) }, null)!;
        await (Task)approve.Invoke(page, new object[] { request })!;
        Dictionary<int, string> errors = (Dictionary<int, string>)typeof(ApprovalInbox).GetField("mErrorsByApprovalId", flags)!.GetValue(page)!;
        Assert.Contains("다시", errors[1]);
        Assert.Equal(string.Empty, typeof(ApprovalInbox).GetField("mMessage", flags)!.GetValue(page));
        Assert.Equal("검토 의견 유지", typeof(ApprovalInbox).GetMethod("getCommentValue", flags)!.Invoke(page, new object[] { ApprovalRequestId.CreateExisting(1) }));
        Assert.Equal(0, ((ApprovalSpy)approvalService).ApprovalCalls);
        Assert.Empty((HashSet<int>)typeof(ApprovalInbox).GetField("mProcessingApprovalIds", flags)!.GetValue(page)!);
        conflicts.ShouldFail = false;
        await (Task)approve.Invoke(page, new object[] { request })!;
        Assert.Equal(1, ((ApprovalSpy)approvalService).ApprovalCalls);
        Assert.Empty((Dictionary<int, string>)typeof(ApprovalInbox).GetField("mErrorsByApprovalId", flags)!.GetValue(page)!);
    }
}
