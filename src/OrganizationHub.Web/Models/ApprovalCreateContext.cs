using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalCreateContext
{
    public IReadOnlyList<DriveFolderReference> SourceFolders { get; set; } = new List<DriveFolderReference>();
    public IReadOnlyList<DriveFolderReference> DestinationFolders { get; set; } = new List<DriveFolderReference>();
    public IReadOnlyList<ApprovalUserOption> ApproverOptions { get; set; } = new List<ApprovalUserOption>();
    public IReadOnlyList<ApprovalUserOption> FinalApproverCandidates { get; set; } = new List<ApprovalUserOption>();
    public IReadOnlyList<ApprovalUserOption> FinalApproverOptions { get; set; } = new List<ApprovalUserOption>();
    public IReadOnlyList<ApprovalStepCount> AvailableStepCounts { get; set; } = new List<ApprovalStepCount>();
    public IReadOnlyList<ApprovalDefaultLineOption> DefaultLineOptions { get; set; } = new List<ApprovalDefaultLineOption>();
    public bool CanCreateRequest
    {
        get; set;
    }
    public EApprovalSetupArea SetupArea
    {
        get; set;
    }
    public string BlockReason { get; set; } = string.Empty;
}
