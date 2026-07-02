using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class WorkGuidelineSectionFormModel
{
    public string ClientKey { get; set; } = Guid.NewGuid().ToString("N");

    public SortableItemKey ClientKeyValue
    {
        get
        {
            return SortableItemKey.CreateRequired(ClientKey);
        }
    }

    public int Id
    {
        get; set;
    }

    public WorkGuidelineSectionId SectionIdValue
    {
        get
        {
            return WorkGuidelineSectionId.CreateOptional(Id);
        }
    }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int SortOrder
    {
        get; set;
    }
}
