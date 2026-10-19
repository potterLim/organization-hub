using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class WorkflowEditorBoundaryTests
{
    [Fact]
    public void AddingFourthStepDoesNotThrowOrDiscardCustomNames()
    {
        AdminApprovalWorkflows editor = new AdminApprovalWorkflows();
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        MethodInfo add = typeof(AdminApprovalWorkflows).GetMethod("addStep", flags)!;
        ApprovalWorkflowFormModel form = (ApprovalWorkflowFormModel)typeof(AdminApprovalWorkflows).GetField("mFormModel", flags)!.GetValue(editor)!;
        add.Invoke(editor, null);
        form.Steps[0].StepName = "회계 확인";
        add.Invoke(editor, null);
        add.Invoke(editor, null);
        add.Invoke(editor, null);
        Assert.Equal(ApprovalStepCount.MAXIMUM_VALUE, form.Steps.Count);
        Assert.Equal("회계 확인", form.Steps[0].StepName);
    }

    [Fact]
    public async Task WorkflowManagementRejectsUnsupportedStepCountBeforeSavingAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        {
            ApprovalWorkflowFormModel form = new ApprovalWorkflowFormModel();
            form.Name = "Unsupported workflow";
            for (int index = 1; index <= 4; index++)
            {
                ApprovalWorkflowStepFormModel step = new ApprovalWorkflowStepFormModel();
                step.StepOrder = index;
                step.StepName = "Approve";
                step.ApproverSelectionType = EApproverSelectionType.SpecificUser;
                step.ApproverUserId = 1;
                form.Steps.Add(step);
            }
            ServiceResult result = await new ApprovalWorkflowManagementService(factory).CreateWorkflowAsync(form, CancellationToken.None);
            Assert.False(result.IsSuccess);
            using (OrganizationHub.Web.Data.OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.ApprovalWorkflows);
            }
        }
    }
}
