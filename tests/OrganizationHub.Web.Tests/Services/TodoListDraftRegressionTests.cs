using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Components.Pages;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoListDraftRegressionTests
{
    [Fact]
    public async Task ChangingListDoesNotDiscardActiveEditWithoutConfirmationAsync()
    {
        Todos page = new Todos();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Todos).GetField("mSelectedTaskListId", flags)!.SetValue(page, "all");
        typeof(Todos).GetField("mEditingTaskId", flags)!.SetValue(page, "task");
        typeof(Todos).GetField("mEditingTitle", flags)!.SetValue(page, "아직 저장하지 않은 제목");
        await (Task)typeof(Todos).GetMethod("handleTaskListChangedAsync", flags)!.Invoke(page, new object[] { "other" })!;
        Assert.Equal("all", typeof(Todos).GetField("mSelectedTaskListId", flags)!.GetValue(page));
        Assert.Equal("아직 저장하지 않은 제목", typeof(Todos).GetField("mEditingTitle", flags)!.GetValue(page));
    }
}
