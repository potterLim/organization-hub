using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Models;

public sealed class WorkGuidelineSnapshot
{
    public required int Revision { get; init; }
    public required IReadOnlyList<WorkGuidelineSection> Sections { get; init; }
}
