using System;

namespace OrganizationHub.Web.Components.Pages;

internal readonly record struct CalendarEditorSchedule
{
    public DateTime StartLocal
    {
        get;
    }

    public DateTime EndLocal
    {
        get;
    }

    public CalendarEditorSchedule(DateTime startLocal, DateTime endLocal)
    {
        if (endLocal < startLocal)
        {
            throw new ArgumentOutOfRangeException(nameof(endLocal), "Calendar editor schedule end cannot be earlier than start.");
        }

        StartLocal = startLocal;
        EndLocal = endLocal;
    }

    public static CalendarEditorSchedule CreateNormalized(DateTime startLocal, DateTime endLocal)
    {
        DateTime normalizedEndLocal = endLocal < startLocal ? startLocal : endLocal;
        return new CalendarEditorSchedule(startLocal, normalizedEndLocal);
    }
}
