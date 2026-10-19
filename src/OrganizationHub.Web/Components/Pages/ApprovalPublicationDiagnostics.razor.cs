using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalPublicationDiagnostics
{
    private ApprovalPublicationPage? mPageOrNull;
    private bool mIsLoading;
    private int mPageNumber = 1;
    private string mMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await tryLoadUserSessionAsync();
        await reloadAsync();
    }

    private async Task reloadAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }
        mIsLoading = true;
        try
        {
            mPageOrNull = await PublicationService.GetPendingPublicationsAsync(CurrentUserSessionOrNull, mPageNumber, CancellationToken.None);
            mPageNumber = mPageOrNull.PageNumber;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Approval publication diagnostics failed.");
            mPageOrNull = null;
            mMessage = "저장 상태를 불러오지 못했습니다. 권한과 연결 상태를 확인한 후 다시 시도해 주세요.";
        }
        finally
        {
            mIsLoading = false;
        }
    }

    private async Task retryAsync(int requestId)
    {
        if (CurrentUserSessionOrNull == null || mIsLoading)
        {
            return;
        }
        mIsLoading = true;
        try
        {
            ServiceResult result = await PublicationService.RetryPublicationAsAdministratorAsync(CurrentUserSessionOrNull, ApprovalRequestId.CreateExisting(requestId), CancellationToken.None);
            mMessage = result.IsSuccess ? "승인본 저장이 완료되었습니다." : result.ErrorMessage;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Manual approval publication retry failed.");
            mMessage = "저장 결과를 확인하지 못했습니다. 저장 상태를 새로고침해 주세요.";
        }
        await reloadAsync();
    }

    private async Task changePageAsync(int offset)
    {
        mPageNumber += offset;
        await reloadAsync();
    }

    private string formatTime(DateTime? valueOrNull)
    {
        return valueOrNull.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(valueOrNull.Value, DateTimeKind.Utc), ClientTimeZoneInfo).ToString("yyyy-MM-dd HH:mm") : "아직 시도하지 않음";
    }
}
