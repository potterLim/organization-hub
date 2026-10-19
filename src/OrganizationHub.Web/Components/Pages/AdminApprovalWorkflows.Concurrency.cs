using System.Linq;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private async Task reloadWorkflowAsync()
    {
        int selectedId = mFormModel.Id;
        await loadDataAsync();
        ApprovalWorkflow? workflowOrNull = mWorkflows.SingleOrDefault(workflow => workflow.Id == selectedId);
        if (workflowOrNull == null)
        {
            clearForm();
        }
        else
        {
            editWorkflow(workflowOrNull);
        }
        mHasSaveConflict = false;
        mMessage = string.Empty;
    }
}
