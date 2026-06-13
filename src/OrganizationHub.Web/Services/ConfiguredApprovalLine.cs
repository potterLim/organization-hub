using System.Collections.Generic;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class ConfiguredApprovalLine
{
    public ConfiguredApprovalLine(int stepCount, IReadOnlyList<ApprovalUserOption> intermediateApprovers, IReadOnlyList<ApprovalUserOption> finalApprovers, ApprovalDefaultLineOption defaultLine)
    {
        StepCount = stepCount;
        IntermediateApprovers = intermediateApprovers;
        FinalApprovers = finalApprovers;
        DefaultLine = defaultLine;
    }

    public int StepCount
    {
        get; private init;
    }

    public IReadOnlyList<ApprovalUserOption> IntermediateApprovers
    {
        get; private init;
    }

    public IReadOnlyList<ApprovalUserOption> FinalApprovers
    {
        get; private init;
    }

    public ApprovalDefaultLineOption DefaultLine
    {
        get; private init;
    }
}
