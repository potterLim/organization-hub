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
            ? "작성자 정보를 저장하지 못했고 Google 항목의 생성 취소도 확인하지 못했습니다. 중복 생성을 피하려면 다시 만들지 말고 관리자에게 참조 " + resourceId + "를 알려 주세요."
            : "작성자 정보를 저장하지 못해 새 항목 생성을 취소했습니다. 잠시 후 다시 시도해 주세요.", cause)
    {
        IsRemoteStateUncertain = isRemoteStateUncertain;
    }
}
