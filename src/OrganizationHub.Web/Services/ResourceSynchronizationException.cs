using System;

namespace OrganizationHub.Web.Services;

internal sealed class ResourceSynchronizationException : Exception
{
    public bool IsWriteConfirmed { get; }
    public ResourceSynchronizationException(Exception cause, bool isWriteConfirmed) : base(isWriteConfirmed
        ? "마감 시간을 저장하는 중입니다."
        : "마감 시간의 저장 여부를 확인하지 못했습니다. 현재 마감 시간을 확인한 뒤 다시 저장해 주세요.", cause)
    {
        IsWriteConfirmed = isWriteConfirmed;
    }
}
