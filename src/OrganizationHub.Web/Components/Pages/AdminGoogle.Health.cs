using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
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
            mShowHealthyHealthItems = false;
            if (mHealthItems.Count == 0)
            {
                GoogleHealthCheckItem healthCheckItem = new GoogleHealthCheckItem();
                healthCheckItem.Area = "진단";
                healthCheckItem.Name = "결과 없음";
                healthCheckItem.Status = "Warning";
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
            healthCheckItem.Status = "Error";
            healthCheckItem.Message = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mHealthItems.Add(healthCheckItem);
            mMessage = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private IReadOnlyList<GoogleHealthCheckItem> getAttentionHealthItems()
    {
        return mHealthItems.Where(item => isHealthyHealthStatus(item.Status) == false).ToList();
    }

    private IReadOnlyList<GoogleHealthCheckItem> getHealthyHealthItems()
    {
        return mHealthItems.Where(item => isHealthyHealthStatus(item.Status)).ToList();
    }

    private bool isHealthyHealthStatus(string status)
    {
        return status == "Healthy";
    }

    private void toggleHealthyHealthItems()
    {
        mShowHealthyHealthItems = !mShowHealthyHealthItems;
    }

    private string getHealthSummaryClass()
    {
        if (mHealthItems.Any(item => item.Status == "Error"))
        {
            return "hub-health-error";
        }

        if (mHealthItems.Any(item => item.Status == "Warning"))
        {
            return "hub-health-warning";
        }

        return "hub-health-healthy";
    }

    private string getHealthSummaryText()
    {
        int errorCount = mHealthItems.Count(item => item.Status == "Error");
        int warningCount = mHealthItems.Count(item => item.Status == "Warning");
        if (errorCount > 0)
        {
            return "오류 " + errorCount + "개";
        }

        if (warningCount > 0)
        {
            return "주의 " + warningCount + "개";
        }

        return "정상";
    }

    private string getHealthSummaryDescription()
    {
        int attentionCount = getAttentionHealthItems().Count;
        int healthyCount = getHealthyHealthItems().Count;
        if (attentionCount > 0)
        {
            return "먼저 확인할 항목 " + attentionCount + "개, 정상 항목 " + healthyCount + "개";
        }

        return "정상 항목 " + healthyCount + "개가 접혀 있습니다.";
    }

    private string getHealthClass(string status)
    {
        switch (status)
        {
            case "Healthy":
                return "hub-health-healthy";
            case "Warning":
                return "hub-health-warning";
            default:
                return "hub-health-error";
        }
    }

    private string getHealthText(string status)
    {
        switch (status)
        {
            case "Healthy":
                return "정상";
            case "Warning":
                return "주의";
            default:
                return "오류";
        }
    }
}
