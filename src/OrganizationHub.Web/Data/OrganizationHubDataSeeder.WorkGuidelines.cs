using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDataSeeder
{
    private async Task seedWorkGuidelineSectionsAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        bool hasSections = await dbContext.WorkGuidelineSections.AnyAsync(cancellationToken);
        if (hasSections)
        {
            return;
        }

        addWorkGuidelineSection(dbContext, "파일 이름", "날짜_팀명_문서주제 형식을 사용합니다.\n예: 2026-05-21_운영팀_주간계획.pdf", 10);
        addWorkGuidelineSection(dbContext, "폴더 관리", "루트 폴더 구조는 관리자 화면에서만 정리합니다.\n각 팀은 자기 팀 폴더 안에서 자료를 관리합니다.", 20);
        addWorkGuidelineSection(dbContext, "파일 올리기", "중복 파일 올리기를 피합니다.\n개인 정보가 포함된 파일은 외부 공유 자료에 올리지 않습니다.", 30);
    }

    private void addWorkGuidelineSection(OrganizationHubDbContext dbContext, string title, string body, int sortOrder)
    {
        WorkGuidelineSection section = new WorkGuidelineSection();
        section.Title = title;
        section.Body = body;
        section.SortOrder = sortOrder;
        section.UpdatedAt = DateTime.UtcNow;
        dbContext.WorkGuidelineSections.Add(section);
    }
}
