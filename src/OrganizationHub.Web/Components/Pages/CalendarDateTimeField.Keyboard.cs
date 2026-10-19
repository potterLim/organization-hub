using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace OrganizationHub.Web.Components.Pages;

public partial class CalendarDateTimeField
{
    private ElementReference mRootElement;
    private IJSObjectReference? mKeyboardModuleOrNull;
    private DotNetObjectReference<CalendarDateTimeField>? mKeyboardReferenceOrNull;
    private bool mWasRenderedOpen;
    private DateTime? mKeyboardFocusDateOrNull;

    [Inject]
    private IJSRuntime JavaScriptRuntime { get; set; } = null!;

    [JSInvokable("ClosePickerFromKeyboard")]
    public Task ClosePickerFromKeyboardAsync()
    {
        return closePickerAsync();
    }

    [JSInvokable("MovePickerDateFromKeyboard")]
    public Task MovePickerDateFromKeyboardAsync(string dateText, string key)
    {
        DateTime date;
        if (IsOpen == false || IsDisabled || DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) == false)
        {
            return Task.CompletedTask;
        }

        DateTime nextDate = key switch
        {
            "ArrowLeft" => date.AddDays(-1),
            "ArrowRight" => date.AddDays(1),
            "ArrowUp" => date.AddDays(-7),
            "ArrowDown" => date.AddDays(7),
            "Home" => date.AddDays(-(int)date.DayOfWeek),
            "End" => date.AddDays(6 - (int)date.DayOfWeek),
            "PageUp" => date.AddMonths(-1),
            "PageDown" => date.AddMonths(1),
            _ => date
        };
        if (createSelectionPolicy().isDateDisabled(nextDate))
        {
            return Task.CompletedTask;
        }

        mVisibleMonth = new DateTime(nextDate.Year, nextDate.Month, CalendarGridPolicy.FIRST_DAY_OF_MONTH);
        mKeyboardFocusDateOrNull = nextDate;
        mDayCells = createDayCells();
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mKeyboardModuleOrNull != null)
            {
                await mKeyboardModuleOrNull.InvokeVoidAsync("disposePicker", mRootElement);
                await mKeyboardModuleOrNull.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            mKeyboardReferenceOrNull?.Dispose();
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        bool isOpen = IsOpen && IsDisabled == false;
        if (isOpen && mWasRenderedOpen == false)
        {
            if (mKeyboardModuleOrNull == null)
            {
                mKeyboardModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubDateTimePicker.js");
            }
            if (mKeyboardReferenceOrNull == null)
            {
                mKeyboardReferenceOrNull = DotNetObjectReference.Create(this);
            }
            await mKeyboardModuleOrNull.InvokeVoidAsync("openPicker", mRootElement, mKeyboardReferenceOrNull);
        }
        else if (isOpen == false && mWasRenderedOpen && mKeyboardModuleOrNull != null)
        {
            await mKeyboardModuleOrNull.InvokeVoidAsync("closePicker", mRootElement);
        }

        mWasRenderedOpen = isOpen;
        if (isOpen && mKeyboardFocusDateOrNull is DateTime focusDate && mKeyboardModuleOrNull != null)
        {
            mKeyboardFocusDateOrNull = null;
            await mKeyboardModuleOrNull.InvokeVoidAsync("focusPickerDate", mRootElement, focusDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

}
