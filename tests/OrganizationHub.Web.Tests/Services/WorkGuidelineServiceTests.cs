using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class WorkGuidelineServiceTests
{
    private const int NEW_SECTION_ID = 0;
    private const int FIRST_SECTION_ID = 1;
    private const int SECOND_SECTION_ID = 2;
    private const int SEEDED_SECTION_COUNT = 2;
    private const int UNKNOWN_SECTION_ID = 99;

    [Fact]
    public async Task SaveSectionsAllowsEmptySectionsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedSectionsAsync(dbContextFactory);
            WorkGuidelineService workGuidelineService = new WorkGuidelineService(dbContextFactory);
            List<WorkGuidelineSectionFormModel> formModels = new List<WorkGuidelineSectionFormModel>();

            ServiceResult result = await workGuidelineService.SaveSectionsAsync(formModels, 0, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Empty(dbContext.WorkGuidelineSections);
            }
        }
    }

    [Fact]
    public async Task SaveSectionsAllowsNineSectionsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            WorkGuidelineService workGuidelineService = new WorkGuidelineService(dbContextFactory);
            List<WorkGuidelineSectionFormModel> formModels = createSectionFormModels(WorkGuidelinePolicy.MAX_SECTION_COUNT);

            ServiceResult result = await workGuidelineService.SaveSectionsAsync(formModels, 0, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Equal(WorkGuidelinePolicy.MAX_SECTION_COUNT, dbContext.WorkGuidelineSections.Count());
            }
        }
    }

    [Fact]
    public async Task SaveSectionsRejectsMoreThanNineSectionsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            WorkGuidelineService workGuidelineService = new WorkGuidelineService(dbContextFactory);
            List<WorkGuidelineSectionFormModel> formModels = createSectionFormModels(WorkGuidelinePolicy.MAX_SECTION_COUNT + 1);

            ServiceResult result = await workGuidelineService.SaveSectionsAsync(formModels, 0, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("업무 기준은 0개부터 9개까지 구성할 수 있습니다.", result.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Empty(dbContext.WorkGuidelineSections);
            }
        }
    }

    [Fact]
    public async Task StaleWholeDocumentSaveDoesNotDeleteAnotherEditorsNewCardAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            await seedSectionsAsync(factory);
            WorkGuidelineService service = new WorkGuidelineService(factory);
            List<WorkGuidelineSectionFormModel> stale = new List<WorkGuidelineSectionFormModel>
            {
                createSectionFormModel(1, "보고", "보고 기준"),
                createSectionFormModel(2, "자료", "자료 기준")
            };
            List<WorkGuidelineSectionFormModel> first = new List<WorkGuidelineSectionFormModel>(stale);
            first.Add(createSectionFormModel(0, "새 기준", "다른 관리자가 추가"));
            Assert.True((await service.SaveSectionsAsync(first, 0, CancellationToken.None)).IsSuccess);
            ServiceResult result = await service.SaveSectionsAsync(stale, 0, CancellationToken.None);
            Assert.False(result.IsSuccess);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(3, db.WorkGuidelineSections.Count());
            }
        }
    }

    [Fact]
    public async Task EmptyDocumentSnapshotCannotRemoveCardsAddedAfterLoadAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            WorkGuidelineService service = new WorkGuidelineService(factory);
            WorkGuidelineSnapshot empty = await service.GetSnapshotAsync(CancellationToken.None);
            Assert.Empty(empty.Sections);
            Assert.True((await service.SaveSectionsAsync(createSectionFormModels(1), empty.Revision, CancellationToken.None)).IsSuccess);
            Assert.True((await service.SaveSectionsAsync(new WorkGuidelineSectionFormModel[0], empty.Revision, CancellationToken.None)).IsConcurrencyConflict);
            WorkGuidelineSnapshot latest = await service.GetSnapshotAsync(CancellationToken.None);
            Assert.Single(latest.Sections);
            Assert.True((await service.SaveSectionsAsync(new WorkGuidelineSectionFormModel[0], latest.Revision, CancellationToken.None)).IsSuccess);
            Assert.Empty((await service.GetSnapshotAsync(CancellationToken.None)).Sections);
        }
    }

    [Theory]
    [InlineData(0, EWorkGuidelineGridLayout.Empty)]
    [InlineData(1, EWorkGuidelineGridLayout.TwoColumns)]
    [InlineData(2, EWorkGuidelineGridLayout.TwoColumns)]
    [InlineData(3, EWorkGuidelineGridLayout.ThreeColumns)]
    [InlineData(4, EWorkGuidelineGridLayout.TwoColumns)]
    [InlineData(5, EWorkGuidelineGridLayout.ThreeColumns)]
    [InlineData(6, EWorkGuidelineGridLayout.ThreeColumns)]
    [InlineData(7, EWorkGuidelineGridLayout.ThreeColumns)]
    [InlineData(8, EWorkGuidelineGridLayout.ThreeColumns)]
    [InlineData(9, EWorkGuidelineGridLayout.ThreeColumns)]
    public void GetDisplayLayoutReturnsExpectedLayout(int sectionCount, EWorkGuidelineGridLayout expectedLayout)
    {
        EWorkGuidelineGridLayout layout = WorkGuidelinePolicy.GetDisplayLayout(sectionCount);

        Assert.Equal(expectedLayout, layout);
    }

    [Fact]
    public void GetDisplayLayoutRejectsMoreThanMaximumSectionCount()
    {
        int sectionCount = WorkGuidelinePolicy.MAX_SECTION_COUNT + 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => WorkGuidelinePolicy.GetDisplayLayout(sectionCount));
    }

    [Fact]
    public async Task SaveSectionsRejectsUnknownExistingSectionIdAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedSectionsAsync(dbContextFactory);
            WorkGuidelineService workGuidelineService = new WorkGuidelineService(dbContextFactory);
            List<WorkGuidelineSectionFormModel> formModels = new List<WorkGuidelineSectionFormModel>
            {
                createSectionFormModel(UNKNOWN_SECTION_ID, "업무 원칙", "정확하게 기록합니다.")
            };

            ServiceResult result = await workGuidelineService.SaveSectionsAsync(formModels, 0, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("저장할 업무 기준을 찾을 수 없습니다.", result.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Equal(SEEDED_SECTION_COUNT, dbContext.WorkGuidelineSections.Count());
            }
        }
    }

    [Fact]
    public async Task SaveSectionsRejectsDuplicateExistingSectionIdsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedSectionsAsync(dbContextFactory);
            WorkGuidelineService workGuidelineService = new WorkGuidelineService(dbContextFactory);
            List<WorkGuidelineSectionFormModel> formModels = new List<WorkGuidelineSectionFormModel>
            {
                createSectionFormModel(FIRST_SECTION_ID, "업무 원칙", "정확하게 기록합니다."),
                createSectionFormModel(FIRST_SECTION_ID, "업무 원칙", "다시 기록합니다.")
            };

            ServiceResult result = await workGuidelineService.SaveSectionsAsync(formModels, 0, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("중복된 업무 기준이 포함되어 있습니다.", result.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Equal(SEEDED_SECTION_COUNT, dbContext.WorkGuidelineSections.Count());
            }
        }
    }

    private static async Task seedSectionsAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.WorkGuidelineSections.Add(createSection(FIRST_SECTION_ID, "보고", "보고 기준", WorkGuidelinePolicy.GetSortOrderForPosition(FIRST_SECTION_ID)));
            dbContext.WorkGuidelineSections.Add(createSection(SECOND_SECTION_ID, "자료", "자료 기준", WorkGuidelinePolicy.GetSortOrderForPosition(SECOND_SECTION_ID)));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static WorkGuidelineSection createSection(int id, string title, string body, int sortOrder)
    {
        WorkGuidelineSection section = new WorkGuidelineSection();
        section.Id = id;
        section.Title = title;
        section.Body = body;
        section.SortOrder = sortOrder;
        return section;
    }

    private static WorkGuidelineSectionFormModel createSectionFormModel(int id, string title, string body)
    {
        WorkGuidelineSectionFormModel formModel = new WorkGuidelineSectionFormModel();
        formModel.Id = id;
        formModel.Title = title;
        formModel.Body = body;
        return formModel;
    }

    private static List<WorkGuidelineSectionFormModel> createSectionFormModels(int sectionCount)
    {
        List<WorkGuidelineSectionFormModel> formModels = new List<WorkGuidelineSectionFormModel>();
        for (int sectionNumber = 1; sectionNumber <= sectionCount; ++sectionNumber)
        {
            formModels.Add(createSectionFormModel(NEW_SECTION_ID, "업무 기준 " + sectionNumber, "업무 기준 내용 " + sectionNumber));
        }

        return formModels;
    }
}
