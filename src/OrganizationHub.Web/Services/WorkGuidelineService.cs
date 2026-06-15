using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class WorkGuidelineService : IWorkGuidelineService
{
    private const int MIN_SECTION_COUNT = 1;
    private const int MAX_SECTION_COUNT = 6;
    private const int SORT_ORDER_STEP = 10;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public WorkGuidelineService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<WorkGuidelineSection>> GetSectionsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<WorkGuidelineSection> sections = await dbContext.WorkGuidelineSections
                .OrderBy(section => section.SortOrder)
                .ThenBy(section => section.Title)
                .ToListAsync(cancellationToken);
            return sections;
        }
    }

    public async Task<ServiceResult> SaveSectionsAsync(IReadOnlyList<WorkGuidelineSectionFormModel> formModels, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = validateSections(formModels);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<int> existingSectionIdValues = formModels
                .Where(formModel => formModel.SectionIdValue.HasValue)
                .Select(formModel => formModel.SectionIdValue.GetRequiredValue())
                .Distinct()
                .ToList();
            List<WorkGuidelineSection> existingSections = await dbContext.WorkGuidelineSections.ToListAsync(cancellationToken);
            Dictionary<int, WorkGuidelineSection> sectionsById = existingSections.ToDictionary(section => section.Id);

            int sortOrder = SORT_ORDER_STEP;
            foreach (WorkGuidelineSectionFormModel formModel in formModels)
            {
                WorkGuidelineSection section = getOrCreateSection(dbContext, sectionsById, formModel);
                section.Title = formModel.Title.Trim();
                section.Body = formModel.Body.Trim();
                section.SortOrder = sortOrder;
                section.UpdatedAt = DateTime.UtcNow;
                sortOrder += SORT_ORDER_STEP;
            }

            foreach (WorkGuidelineSection section in existingSections)
            {
                if (existingSectionIdValues.Contains(section.Id) == false)
                {
                    dbContext.WorkGuidelineSections.Remove(section);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private static ServiceResult validateSections(IReadOnlyList<WorkGuidelineSectionFormModel> formModels)
    {
        if (formModels.Count < MIN_SECTION_COUNT || formModels.Count > MAX_SECTION_COUNT)
        {
            return ServiceResult.Fail("업무 기준은 1개부터 6개까지 구성할 수 있습니다.");
        }

        foreach (WorkGuidelineSectionFormModel formModel in formModels)
        {
            if (string.IsNullOrWhiteSpace(formModel.Title))
            {
                return ServiceResult.Fail("각 업무 기준에는 제목이 필요합니다.");
            }
        }

        return ServiceResult.Succeed();
    }

    private static WorkGuidelineSection getOrCreateSection(
        OrganizationHubDbContext dbContext,
        IReadOnlyDictionary<int, WorkGuidelineSection> sectionsById,
        WorkGuidelineSectionFormModel formModel
    )
    {
        if (formModel.SectionIdValue.HasValue)
        {
            int sectionIdValue = formModel.SectionIdValue.GetRequiredValue();
            WorkGuidelineSection? sectionOrNull;
            if (sectionsById.TryGetValue(sectionIdValue, out sectionOrNull))
            {
                return sectionOrNull;
            }
        }

        WorkGuidelineSection createdSection = new WorkGuidelineSection();
        dbContext.WorkGuidelineSections.Add(createdSection);
        return createdSection;
    }

}
