using System.Diagnostics;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalHistory
{
    private string getSearchSummary()
    {
        string status;
        switch (mHistoryFilter)
        {
            case EApprovalHistoryFilter.MyOngoing:
                status = "내 진행 중 요청";
                break;
            case EApprovalHistoryFilter.Approved:
                status = "완료";
                break;
            case EApprovalHistoryFilter.Rejected:
                status = "반려";
                break;
            case EApprovalHistoryFilter.AllRelated:
                status = "전체 상태";
                break;
            default:
                Debug.Fail("Unexpected history filter: " + mHistoryFilter);
                status = "전체 상태";
                break;
        }
        string period = mRequestedDateRangeOrNull == null ? string.Empty : " · 기간 설정됨";
        return "상태·기간 상세 검색 · " + status + period;
    }

    private Task resetSearchAsync()
    {
        mSearchKeyword = string.Empty;
        mRequestedDateRangeOrNull = null;
        return showHistoryAsync(false);
    }
}
