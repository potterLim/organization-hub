using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.BrowserFixture;

public sealed class CaptureHealth : IGoogleHealthCheckService
{
    public Task<IReadOnlyList<GoogleHealthCheckItem>> GetHealthCheckItemsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<GoogleHealthCheckItem>>(new GoogleHealthCheckItem[] { new GoogleHealthCheckItem { Area = "연결", Name = "Google 계정", Status = CaptureState.Mode == "disconnected" ? EGoogleHealthStatus.Error : EGoogleHealthStatus.Healthy, Message = CaptureState.Mode == "disconnected" ? "조직 운영용 Google 계정을 연결해 주세요." : "Google 계정이 연결되어 있습니다." }, new GoogleHealthCheckItem { Area = "Drive", Name = "조직 자료 위치", Status = EGoogleHealthStatus.Healthy, Message = "자료 위치와 접근 권한이 준비되어 있습니다." }, new GoogleHealthCheckItem { Area = "Calendar", Name = "팀 캘린더", Status = CaptureState.Mode == "health-warning" ? EGoogleHealthStatus.Warning : EGoogleHealthStatus.Healthy, Message = CaptureState.Mode == "health-warning" ? "일부 팀의 캘린더 연결을 확인해 주세요." : "캘린더가 연결되어 있습니다." } });
}
