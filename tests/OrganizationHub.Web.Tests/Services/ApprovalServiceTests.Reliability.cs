using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    private sealed class ApprovalReliabilityFixture : IDisposable
    {
        private readonly MemoryCache mMemoryCache = new MemoryCache(new MemoryCacheOptions());

        public ApprovalSaveInterceptor Interceptor { get; } = new ApprovalSaveInterceptor();
        public TestDbContextFactory DbContextFactory
        {
            get;
        }
        public FakeDriveService DriveService { get; } = createApprovalDriveService();
        public UserSession Requester { get; } = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
        public UserSession Leader { get; } = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
        public UserSession Manager { get; } = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, 2);

        public ApprovalReliabilityFixture()
        {
            DbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover, new TestDbContextFactory(Interceptor));
            DriveService.SetFileContent("planning-file", Encoding.UTF8.GetBytes("submitted version"));
        }

        public ApprovalService CreateService()
        {
            OrganizationPermissionService organizationPermissions = new OrganizationPermissionService(DbContextFactory);
            PermissionService permissions = new PermissionService(DbContextFactory, DriveService, mMemoryCache, organizationPermissions);
            return new ApprovalService(DbContextFactory, permissions, DriveService, organizationPermissions);
        }

        public async Task<ApprovalRequestId> SubmitAsync()
        {
            ServiceResult submission = await CreateService().SubmitRequestAsync(Requester, createRegressionApprovalForm().CreateCommand(), CancellationToken.None);
            Assert.True(submission.IsSuccess, submission.ErrorMessage);
            using (OrganizationHubDbContext dbContext = DbContextFactory.CreateDbContext())
            {
                return ApprovalRequestId.CreateExisting(dbContext.ApprovalRequests.Single().Id);
            }
        }

        public async Task ApproveFirstStepAsync(ApprovalRequestId requestId)
        {
            ServiceResult approval = await CreateService().ApproveRequestAsync(Leader, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("검토 완료"), CancellationToken.None);
            Assert.True(approval.IsSuccess, approval.ErrorMessage);
        }

        public async Task AssertCompletedOnceAsync(ApprovalRequestId requestId)
        {
            using (OrganizationHubDbContext dbContext = DbContextFactory.CreateDbContext())
            {
                ApprovalRequest request = dbContext.ApprovalRequests.Include(item => item.AuditLogs).Include(item => item.Steps).Single();
                Assert.Equal(EApprovalRequestStatus.Approved, request.Status);
                Assert.NotNull(request.CompletedAt);
                Assert.Equal(3, request.AuditLogs.Count);
                Assert.All(request.Steps, step => Assert.Equal(EApprovalStepStatus.Approved, step.Status));
                Assert.Single(dbContext.UploadAuditLogs);
                ApprovalPublication publication = Assert.Single(dbContext.ApprovalPublications);
                Assert.True(publication.IsCompleted);
                Assert.Equal(publication.TargetFileId, request.ApprovedFileId);
                FileDownloadResult file = await DriveService.DownloadFileSnapshotAsync(DriveItemId.CreateRequired(request.ApprovedFileId), CancellationToken.None);
                using (Stream content = file.ContentStream)
                using (StreamReader reader = new StreamReader(content))
                {
                    Assert.Equal("submitted version", await reader.ReadToEndAsync());
                }
            }

            Assert.Equal(1, DriveService.PublicationWriteCount);
            ServiceResult duplicateRecovery = await CreateService().PublishApprovedFileAsync(requestId, CancellationToken.None);
            Assert.True(duplicateRecovery.IsSuccess);
            Assert.Equal(1, DriveService.PublicationWriteCount);
        }

        public void Dispose()
        {
            mMemoryCache.Dispose();
            DbContextFactory.Dispose();
        }
    }

    [Fact]
    public async Task SubmissionSnapshotSurvivesSourceAndApprovedFileChangesAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            fixture.DriveService.SetFileContent("planning-file", Encoding.UTF8.GetBytes("changed after submission"));
            await assertSnapshotTextAsync(fixture.CreateService(), fixture.Leader, requestId, "submitted version");
            Assert.Null(await fixture.CreateService().GetDocumentSnapshotOrNullAsync(fixture.Manager, requestId, CancellationToken.None));
            UserSession unrelated = createSession(5, "other", "무관한 사용자", EStoredCompatibilityRole.ManagementDeputyDirector, 2);
            Assert.Null(await fixture.CreateService().GetDocumentSnapshotOrNullAsync(unrelated, requestId, CancellationToken.None));
            await fixture.ApproveFirstStepAsync(requestId);
            await assertSnapshotTextAsync(fixture.CreateService(), fixture.Manager, requestId, "submitted version");
            ServiceResult approval = await fixture.CreateService().ApproveRequestAsync(fixture.Manager, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("최종 확인"), CancellationToken.None);
            Assert.True(approval.IsSuccess, approval.ErrorMessage);
            await fixture.AssertCompletedOnceAsync(requestId);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                string approvedFileId = dbContext.ApprovalRequests.Single().ApprovedFileId;
                fixture.DriveService.SetFileContent(approvedFileId, Encoding.UTF8.GetBytes("changed after completion"));
            }

            await assertSnapshotTextAsync(fixture.CreateService(), fixture.Requester, requestId, "submitted version");
        }
    }

    [Theory]
    [InlineData(EApprovalAuditAction.Approved, EApprovalAuditAction.Approved)]
    [InlineData(EApprovalAuditAction.Approved, EApprovalAuditAction.Rejected)]
    [InlineData(EApprovalAuditAction.Rejected, EApprovalAuditAction.Approved)]
    public async Task ConcurrentDecisionsCommitOnlyOneTransitionAsync(EApprovalAuditAction firstAction, EApprovalAuditAction secondAction)
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            fixture.Interceptor.PauseNextDecision();
            Task<ServiceResult> firstDecision = executeRegressionDecisionAsync(fixture.CreateService(), fixture.Leader, requestId, firstAction);
            await fixture.Interceptor.DecisionPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                ServiceResult secondDecision = await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Leader, requestId, secondAction);
                Assert.True(secondDecision.IsSuccess, secondDecision.ErrorMessage);
            }
            finally
            {
                fixture.Interceptor.ContinueDecision.TrySetResult(true);
            }

            Assert.False((await firstDecision).IsSuccess);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                ApprovalRequest request = dbContext.ApprovalRequests.Include(item => item.AuditLogs).Include(item => item.Steps).Single();
                Assert.Equal(2, request.AuditLogs.Count);
                Assert.Equal(1, request.Revision);
                Assert.Equal(secondAction == EApprovalAuditAction.Approved ? EApprovalStepStatus.Approved : EApprovalStepStatus.Rejected, request.Steps.Single(step => step.StepNumber == 1).Status);
                Assert.Empty(dbContext.ApprovalPublications);
            }
        }
    }

    [Fact]
    public async Task ConcurrentFinalApprovalAndRejectionCannotDuplicatePublicationAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.PublicationStartedOrNull = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.DriveService.ContinuePublicationOrNull = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<ServiceResult> finalApproval = executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved);
            await fixture.DriveService.PublicationStartedOrNull.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                ServiceResult duplicateApproval = await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved);
                ServiceResult rejection = await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Rejected);
                Assert.False(duplicateApproval.IsSuccess);
                Assert.False(rejection.IsSuccess);
            }
            finally
            {
                fixture.DriveService.ContinuePublicationOrNull.TrySetResult(true);
            }

            Assert.True((await finalApproval).IsSuccess);
            await fixture.AssertCompletedOnceAsync(requestId);
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
        }
    }

    [Theory]
    [InlineData(FakeDriveService.EPublicationFailure.BeforeUpload, EDriveFileNameConflictResolution.AddNewFile)]
    [InlineData(FakeDriveService.EPublicationFailure.AfterUpload, EDriveFileNameConflictResolution.AddNewFile)]
    [InlineData(FakeDriveService.EPublicationFailure.BeforeUpload, EDriveFileNameConflictResolution.ReplaceExistingFile)]
    [InlineData(FakeDriveService.EPublicationFailure.AfterUpload, EDriveFileNameConflictResolution.ReplaceExistingFile)]
    public async Task PublicationRetryUsesPersistedTargetAndOriginalDocumentAsync(FakeDriveService.EPublicationFailure failure, EDriveFileNameConflictResolution conflictResolution)
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.AddFile("existing-approved-file", "2026-05-23_기획팀_부스운영안.pdf", "application/pdf", "planning-approved-folder", "official-root");
            fixture.DriveService.FailNextPublication(failure);
            ServiceResult finalApproval = await fixture.CreateService().ApproveRequestAsync(fixture.Manager, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("최종 확인"), conflictResolution, CancellationToken.None);
            Assert.False(finalApproval.IsSuccess);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                ApprovalRequest request = dbContext.ApprovalRequests.Single();
                Assert.Equal(EApprovalRequestStatus.Publishing, request.Status);
                Assert.Empty(request.ApprovedFileId);
                Assert.Null(request.CompletedAt);
                Assert.Empty(dbContext.UploadAuditLogs);
                ApprovalPublication publication = Assert.Single(dbContext.ApprovalPublications);
                Assert.False(publication.IsCompleted);
                Assert.Equal(conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile, publication.IsReplacement);
                Assert.Equal(conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile ? "2026-05-23_기획팀_부스운영안.pdf" : "2026-05-23_기획팀_부스운영안 (2).pdf", publication.FileName);
            }

            fixture.DriveService.SetFileContent("planning-file", Encoding.UTF8.GetBytes("changed before retry"));
            ServiceResult recovery = await fixture.CreateService().PublishApprovedFileAsync(requestId, CancellationToken.None);
            Assert.True(recovery.IsSuccess, recovery.ErrorMessage);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task CompletionDatabaseFailureReconcilesExistingPublicationAfterRestartAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.Interceptor.FailNextCompletion();
            ServiceResult finalApproval = await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved);
            Assert.False(finalApproval.IsSuccess);
            Assert.Equal(1, fixture.DriveService.PublicationWriteCount);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                Assert.Equal(EApprovalRequestStatus.Publishing, dbContext.ApprovalRequests.Single().Status);
                Assert.Empty(dbContext.UploadAuditLogs);
                Assert.False(dbContext.ApprovalPublications.Single().IsCompleted);
            }

            ServiceResult recovery = await fixture.CreateService().PublishApprovedFileAsync(requestId, CancellationToken.None);
            Assert.True(recovery.IsSuccess, recovery.ErrorMessage);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task ExpiredPublicationLeaseCanBeRecoveredAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.FailNextPublication(FakeDriveService.EPublicationFailure.AfterUpload);
            await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                ApprovalPublication publication = dbContext.ApprovalPublications.Single();
                publication.LeaseToken = "interrupted-process";
                publication.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5);
                await dbContext.SaveChangesAsync();
            }

            Assert.False((await fixture.CreateService().PublishApprovedFileAsync(requestId, CancellationToken.None)).IsSuccess);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                dbContext.ApprovalPublications.Single().LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await dbContext.SaveChangesAsync();
            }

            Assert.True((await fixture.CreateService().PublishApprovedFileAsync(requestId, CancellationToken.None)).IsSuccess);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task ConcurrentFinalDecisionsCannotPublishBeforeWinningDatabaseCommitAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.Interceptor.PauseNextDecision();
            Task<ServiceResult> firstApproval = executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved);
            await fixture.Interceptor.DecisionPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                Assert.True((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            }
            finally
            {
                fixture.Interceptor.ContinueDecision.TrySetResult(true);
            }

            Assert.False((await firstApproval).IsSuccess);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task FailedPublicationIntentDoesNotWriteRemoteFileOrApprovalAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.Interceptor.FailNextIntent();
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            Assert.Equal(0, fixture.DriveService.PublicationWriteCount);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                Assert.Equal(EApprovalRequestStatus.Pending, dbContext.ApprovalRequests.Single().Status);
                Assert.Equal(2, dbContext.ApprovalAuditLogs.Count());
                Assert.Empty(dbContext.ApprovalPublications);
            }

            Assert.True((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task BackgroundWorkerRecoversPublicationWithoutUserRetryAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.FailNextPublication(FakeDriveService.EPublicationFailure.AfterUpload);
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                dbContext.ApprovalPublications.Single().NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
                await dbContext.SaveChangesAsync();
            }

            ServiceCollection services = new ServiceCollection();
            services.AddScoped<IApprovalPublicationService>(serviceProvider => fixture.CreateService());
            using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            using (ApprovalPublicationWorker worker = new ApprovalPublicationWorker(fixture.DbContextFactory, serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApprovalPublicationWorker>.Instance))
            {
                await worker.StartAsync(CancellationToken.None);
                try
                {
                    await fixture.Interceptor.CompletionSaved.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    await worker.StopAsync(CancellationToken.None);
                }
            }

            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task LegacyPendingRequestCannotApproveUnpreservedCurrentFileAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                dbContext.ApprovalRequests.Single().DocumentHash = string.Empty;
                dbContext.ApprovalDocuments.RemoveRange(dbContext.ApprovalDocuments);
                await dbContext.SaveChangesAsync();
            }

            ServiceResult approval = await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Leader, requestId, EApprovalAuditAction.Approved);
            Assert.False(approval.IsSuccess);
            Assert.Contains("새로 상신", approval.ErrorMessage);
            Assert.Equal(0, fixture.DriveService.PublicationWriteCount);
        }
    }

    [Fact]
    public async Task SnapshotIntegrityFailureNeverPublishesChangedBytesAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                dbContext.ApprovalDocuments.Single().Content = Encoding.UTF8.GetBytes("corrupt snapshot");
                await dbContext.SaveChangesAsync();
            }

            Assert.Null(await fixture.CreateService().GetDocumentSnapshotOrNullAsync(fixture.Manager, requestId, CancellationToken.None));
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            Assert.Equal(0, fixture.DriveService.PublicationWriteCount);
        }
    }

    private static async Task assertSnapshotTextAsync(ApprovalService approvalService, UserSession userSession, ApprovalRequestId requestId, string expectedText)
    {
        FileDownloadResult? documentOrNull = await approvalService.GetDocumentSnapshotOrNullAsync(userSession, requestId, CancellationToken.None);
        Assert.NotNull(documentOrNull);
        using (Stream contentStream = documentOrNull.ContentStream)
        using (StreamReader reader = new StreamReader(contentStream))
        {
            Assert.Equal(expectedText, await reader.ReadToEndAsync());
        }
    }
}
