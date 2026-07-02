using System.Threading.Tasks;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private async Task setTaskSortModeAsync(ETodoTaskSortMode sortMode)
    {
        if (sortMode == ETodoTaskSortMode.ManualOrder && canUseManualTaskSort() == false)
        {
            sortMode = ETodoTaskSortMode.DueDateAscending;
        }

        if (mTaskSortMode == sortMode)
        {
            return;
        }

        mTaskSortMode = sortMode;
        mShouldInitializeSortable = true;
        if (sortMode != ETodoTaskSortMode.ManualOrder)
        {
            await disposeSortableAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ensureAvailableTaskSortModeAsync()
    {
        if (mTaskSortMode != ETodoTaskSortMode.ManualOrder || canUseManualTaskSort())
        {
            return;
        }

        mTaskSortMode = ETodoTaskSortMode.DueDateAscending;
        await disposeSortableAsync();
    }
}
