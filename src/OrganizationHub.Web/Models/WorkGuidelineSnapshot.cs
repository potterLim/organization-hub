using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class WorkGuidelineSnapshot
{
    public required int Revision
    {
        get; init;
    }
    public required IReadOnlyList<WorkGuidelineSectionItem> Sections
    {
        get; init;
    }
}
