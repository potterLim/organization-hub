using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal static class AdministrativeSaveUtility
{
    public static ServiceResult CreateConflictResult()
    {
        return ServiceResult.Conflict("다른 관리자가 정보를 변경했습니다. 최신 정보를 불러온 뒤 다시 수정해 주세요.");
    }

    public static async Task<ServiceResult> SaveAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
        catch (DbUpdateConcurrencyException)
        {
            return CreateConflictResult();
        }
    }
}
