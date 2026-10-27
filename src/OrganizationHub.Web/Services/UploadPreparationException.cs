using System;

namespace OrganizationHub.Web.Services;

internal sealed class UploadPreparationException : Exception
{
    public UploadPreparationException(Exception cause) : base("파일을 올리지 못했습니다. 잠시 후 다시 시도해 주세요.", cause)
    {
    }
}
