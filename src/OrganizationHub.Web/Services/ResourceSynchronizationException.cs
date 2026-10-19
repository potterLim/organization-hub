using System;

namespace OrganizationHub.Web.Services;

internal sealed class ResourceSynchronizationException : Exception
{
    public ResourceSynchronizationException(Exception cause) : base("Google 항목은 변경됐지만 마감일 기록을 아직 저장하지 못했습니다. 복구 작업을 보관했습니다. 새로고침해 확인해 주세요.", cause)
    {
    }
}
