using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

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

        IReadOnlyList<WorkGuidelineSeedSection> seedSections = createDefaultWorkGuidelineSeedSections();
        foreach (WorkGuidelineSeedSection seedSection in seedSections)
        {
            addWorkGuidelineSection(dbContext, seedSection);
        }
    }

    private IReadOnlyList<WorkGuidelineSeedSection> createDefaultWorkGuidelineSeedSections()
    {
        return new List<WorkGuidelineSeedSection>
        {
            new WorkGuidelineSeedSection
            {
                Title = "파일 이름",
                Body = "날짜_팀명_문서주제 형식을 사용합니다.\n예: 2026-05-21_운영팀_주간계획.pdf",
                SortOrder = WorkGuidelinePolicy.GetSortOrderForPosition(1)
            },
            new WorkGuidelineSeedSection
            {
                Title = "폴더 관리",
                Body = "루트 폴더 구조는 관리자 화면에서만 정리합니다.\n각 팀은 자기 팀 폴더 안에서 자료를 관리합니다.",
                SortOrder = WorkGuidelinePolicy.GetSortOrderForPosition(2)
            },
            new WorkGuidelineSeedSection
            {
                Title = "파일 올리기",
                Body = "중복 파일 올리기를 피합니다.\n개인 정보가 포함된 파일은 외부 공유 자료에 올리지 않습니다.",
                SortOrder = WorkGuidelinePolicy.GetSortOrderForPosition(3)
            }
        };
    }

    private void addWorkGuidelineSection(OrganizationHubDbContext dbContext, WorkGuidelineSeedSection seedSection)
    {
        WorkGuidelineSection section = new WorkGuidelineSection();
        section.Title = seedSection.Title;
        section.Body = seedSection.Body;
        section.SortOrder = seedSection.SortOrder;
        section.UpdatedAt = DateTime.UtcNow;
        dbContext.WorkGuidelineSections.Add(section);
    }
}
