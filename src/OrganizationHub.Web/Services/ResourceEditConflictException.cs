using System;

namespace OrganizationHub.Web.Services;

internal sealed class ResourceEditConflictException : Exception
{
    public ResourceEditConflictException() : base("다른 사용자가 항목을 변경했습니다. 입력값을 확인한 뒤 새로고침하여 최신 항목을 다시 열어 주세요.")
    {
    }
}
