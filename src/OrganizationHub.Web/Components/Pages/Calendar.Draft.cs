using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private CalendarEventFormModel? mInitialEditorDraftOrNull;
    private bool mIsDiscardConfirmationOpen;

    private void rememberEditorDraft()
    {
        CalendarEventFormModel draft = new CalendarEventFormModel();
        draft.CalendarId = mEditorCalendarId;
        draft.Summary = mEditorSummary;
        draft.Description = mEditorDescription;
        draft.Location = mEditorLocation;
        draft.StartLocal = mEditorStartLocalOrNull.GetValueOrDefault();
        draft.EndLocal = mEditorEndLocalOrNull.GetValueOrDefault();
        draft.IsAllDay = mIsEditorAllDay;
        mInitialEditorDraftOrNull = draft;
    }

    private bool hasEditorChanges()
    {
        CalendarEventFormModel? initialOrNull = mInitialEditorDraftOrNull;
        if (initialOrNull == null)
        {
            return true;
        }
        return initialOrNull.CalendarId != mEditorCalendarId || initialOrNull.Summary != mEditorSummary
            || initialOrNull.Description != mEditorDescription || initialOrNull.Location != mEditorLocation
            || initialOrNull.StartLocal != mEditorStartLocalOrNull || initialOrNull.EndLocal != mEditorEndLocalOrNull
            || initialOrNull.IsAllDay != mIsEditorAllDay;
    }

    private void requestCloseEditor()
    {
        if (hasEditorChanges())
        {
            mIsDiscardConfirmationOpen = true;
            return;
        }
        closeEditor();
    }

    private void keepEditorDraft()
    {
        mIsDiscardConfirmationOpen = false;
    }
}
