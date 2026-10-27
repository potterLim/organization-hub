using System;

namespace OrganizationHub.Web.Services;

internal static class ApprovalPublicationMessages
{
    public const string RESULT_UNCONFIRMED = "승인본 저장 결과를 확인하지 못했습니다. 잠시 후 저장 상태를 새로고침해 주세요.";

    public static string GetUserMessage(string message)
    {
        if (message.Contains("서버 로그", StringComparison.Ordinal))
        {
            return RESULT_UNCONFIRMED;
        }
        return message;
    }
}
