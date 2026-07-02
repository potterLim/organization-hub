using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private async Task loadHealthCheckAsync()
    {
        try
        {
            IReadOnlyList<GoogleHealthCheckItem> healthItems = await GoogleHealthCheckService.GetHealthCheckItemsAsync(CancellationToken.None);
            mHealthItems = healthItems.ToList();
            mShouldShowHealthyItems = false;
            if (mHealthItems.Count == 0)
            {
                GoogleHealthCheckItem healthCheckItem = new GoogleHealthCheckItem();
                healthCheckItem.Area = "진단";
                healthCheckItem.Name = "결과 없음";
                healthCheckItem.Status = EGoogleHealthStatus.Warning;
                healthCheckItem.Message = "진단 항목을 만들지 못했습니다. Google 연결 정보와 연결 상태를 새로고침해 주세요.";
                mHealthItems.Add(healthCheckItem);
            }
        }
        catch (Exception exception)
        {
            mHealthItems = new List<GoogleHealthCheckItem>();
            GoogleHealthCheckItem healthCheckItem = new GoogleHealthCheckItem();
            healthCheckItem.Area = "진단";
            healthCheckItem.Name = "실행 오류";
            healthCheckItem.Status = EGoogleHealthStatus.Error;
            healthCheckItem.Message = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mHealthItems.Add(healthCheckItem);
            mMessage = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private void toggleHealthyItemsVisibility()
    {
        mShouldShowHealthyItems = !mShouldShowHealthyItems;
    }
}
