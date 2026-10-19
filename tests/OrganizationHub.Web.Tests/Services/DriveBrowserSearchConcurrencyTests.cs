using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DriveBrowserSearchConcurrencyTests
{
    public class BrowserProxy : DispatchProxy
    {
        public Queue<TaskCompletionSource<IReadOnlyList<DriveFileItem>>> Responses { get; } = new Queue<TaskCompletionSource<IReadOnlyList<DriveFileItem>>>();
        protected override object? Invoke(MethodInfo? targetMethodOrNull, object?[]? argsOrNull)
        {
            return Responses.Dequeue().Task;
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task OlderLoadCannotReplaceResultsErrorsOrLoadingStateAsync(bool shouldFinishLatestFirst, bool shouldFailOld)
    {
        IDriveBrowserService service = DispatchProxy.Create<IDriveBrowserService, BrowserProxy>();
        BrowserProxy proxy = (BrowserProxy)(object)service;
        TaskCompletionSource<IReadOnlyList<DriveFileItem>> first = new TaskCompletionSource<IReadOnlyList<DriveFileItem>>();
        TaskCompletionSource<IReadOnlyList<DriveFileItem>> latest = new TaskCompletionSource<IReadOnlyList<DriveFileItem>>();
        proxy.Responses.Enqueue(first);
        proxy.Responses.Enqueue(latest);
        DriveFileBrowser component = new DriveFileBrowser();
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, OperationPermissionRegressionTests.createSession());
        typeof(DriveFileBrowser).GetProperty("DriveBrowserService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, service);
        typeof(DriveFileBrowser).GetProperty("ChangeVersionService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, new ChangeVersionService());
        typeof(DriveFileBrowser).GetField("mSelectedRootFolderId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, "root");
        MethodInfo load = typeof(DriveFileBrowser).GetMethod("loadCurrentFolderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task oldLoad = (Task)load.Invoke(component, null)!;
        Task latestLoad = (Task)load.Invoke(component, null)!;
        DriveFileItem item = new DriveFileItem();
        item.Id = "latest";
        if (shouldFinishLatestFirst)
        {
            latest.SetResult(new DriveFileItem[] { item });
            await latestLoad;
        }
        if (shouldFailOld)
        {
            first.SetException(new InvalidOperationException("Older load failed"));
        }
        else
        {
            first.SetResult(Array.Empty<DriveFileItem>());
        }
        await oldLoad;
        if (shouldFinishLatestFirst == false)
        {
            Assert.True((bool)typeof(DriveFileBrowser).GetField("mIsLoadingFiles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!);
            latest.SetResult(new DriveFileItem[] { item });
        }
        await latestLoad;
        Assert.Single((List<DriveFileItem>)typeof(DriveFileBrowser).GetField("mFiles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!);
        Assert.Equal("", typeof(DriveFileBrowser).GetField("mFileErrorMessage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component));
    }
}
