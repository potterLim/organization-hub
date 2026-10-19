using System.Threading;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    public Task RecoverDeadlineChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
