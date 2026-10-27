using System;

namespace OrganizationHub.Web.Services;

internal sealed class ResourceDeletionUncertainException : Exception
{
    public ResourceDeletionUncertainException(Exception cause) : base("삭제 여부를 확인하고 있습니다. 같은 항목을 다시 삭제하지 않아도 됩니다.", cause)
    {
    }
}
