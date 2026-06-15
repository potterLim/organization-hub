using System;

namespace OrganizationHub.Web.Data.Entities;

public class WorkGuidelineSection
{
    public int Id
    {
        get; set;
    }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public int SortOrder
    {
        get; set;
    }

    public DateTime UpdatedAt
    {
        get; set;
    }
}
