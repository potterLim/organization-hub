using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ApprovalHistorySearchConcurrencyTests
{
    public class HistoryProxy : DispatchProxy
    {
        public Queue<TaskCompletionSource<ApprovalHistoryPage>> Responses { get; } = new Queue<TaskCompletionSource<ApprovalHistoryPage>>();

        protected override object? Invoke(MethodInfo? targetMethodOrNull, object?[]? argsOrNull)
        {
            return Responses.Dequeue().Task;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OlderSearchCannotReplaceNewerResultsOrClearItsLoadingStateAsync(bool shouldFinishLatestFirst)
    {
        IApprovalService service = DispatchProxy.Create<IApprovalService, HistoryProxy>();
        HistoryProxy proxy = (HistoryProxy)(object)service;
        TaskCompletionSource<ApprovalHistoryPage> first = new TaskCompletionSource<ApprovalHistoryPage>();
        TaskCompletionSource<ApprovalHistoryPage> latest = new TaskCompletionSource<ApprovalHistoryPage>();
        proxy.Responses.Enqueue(first);
        proxy.Responses.Enqueue(latest);
        ApprovalHistory component = new ApprovalHistory();
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, OperationPermissionRegressionTests.createSession());
        typeof(ApprovalHistory).GetProperty("ApprovalService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, service);
        MethodInfo reload = typeof(ApprovalHistory).GetMethod("reloadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task oldLoad = (Task)reload.Invoke(component, null)!;
        Task latestLoad = (Task)reload.Invoke(component, null)!;
        if (shouldFinishLatestFirst)
        {
            latest.SetResult(createPage(2));
            await latestLoad;
            first.SetResult(createPage(105));
        }
        else
        {
            first.SetResult(createPage(105));
            await oldLoad;
            Assert.True((bool)typeof(ApprovalHistory).GetField("mIsLoading", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!);
            latest.SetResult(createPage(2));
        }
        await Task.WhenAll(oldLoad, latestLoad);
        Assert.Equal(2, (int)typeof(ApprovalHistory).GetField("mTotalCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!);
    }

    private static ApprovalHistoryPage createPage(int count)
    {
        return new ApprovalHistoryPage { Items = Array.Empty<ApprovalRequestItem>(), PageNumber = 1, TotalPages = 1, TotalCount = count, MyOngoingCount = 0 };
    }
}
