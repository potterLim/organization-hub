using Microsoft.AspNetCore.Components;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private int mSelectedSettingsTabIndex;

    [SupplyParameterFromQuery(Name = "section")]
    public string? SectionOrNull
    {
        get; set;
    }

    protected override void OnParametersSet()
    {
        mSelectedSettingsTabIndex = getSettingsTabIndex(SectionOrNull);
    }

    private static int getSettingsTabIndex(string? sectionOrNull)
    {
        if (sectionOrNull == "drive")
        {
            return 1;
        }

        if (sectionOrNull == "calendar")
        {
            return 2;
        }

        if (sectionOrNull == "tasks")
        {
            return 3;
        }

        return 0;
    }
}
