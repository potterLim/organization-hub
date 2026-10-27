using System;

namespace OrganizationHub.Web.Services;

internal sealed class ResourceCreationException : Exception
{
    public bool IsRemoteStateUncertain
    {
        get;
    }

    public ResourceCreationException(string resourceId, bool isRemoteStateUncertain, Exception cause) : base(
        isRemoteStateUncertain
            ? "저장 여부를 확인하지 못했습니다. 중복 생성을 피하려면 목록을 확인한 뒤 다시 추가해 주세요."
            : "저장하지 못했습니다. 작성한 내용은 유지됩니다. 다시 시도해 주세요.", cause)
    {
        IsRemoteStateUncertain = isRemoteStateUncertain;
    }
}
