using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AdministrationConcurrencyRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleUserFormCannotRestoreRevokedPositionOrActivationAsync(bool shouldDeactivate)
    {
        using (TestDbContextFactory factory = createFactory())
        {
            UserManagementService service = new UserManagementService(factory, new Pbkdf2PasswordHashService());
            UserSession administrator = createAdministratorSession(factory);
            UserAccountFormModel first = createUserForm();
            first.PositionId = 2;
            first.IsActive = shouldDeactivate == false;
            UserAccountFormModel stale = createUserForm();
            stale.DisplayName = "이름만 바꾸려던 관리자";

            Assert.True((await service.UpdateUserAsync(administrator, first, CancellationToken.None)).IsSuccess);
            ServiceResult conflict = await service.UpdateUserAsync(administrator, stale, CancellationToken.None);

            Assert.False(conflict.IsSuccess);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                UserAccount user = db.UserAccounts.Single(account => account.Id == 1);
                Assert.Equal(2, user.PositionId);
                Assert.Equal(shouldDeactivate == false, user.IsActive);
                Assert.Equal("검증 사용자", user.DisplayName);
            }
        }
    }

    [Fact]
    public async Task StalePositionFormCannotRestoreRevokedPermissionAsync()
    {
        using (TestDbContextFactory factory = createFactory())
        {
            PositionManagementService service = new PositionManagementService(factory);
            PositionFormModel first = createPositionForm(false);
            PositionFormModel stale = createPositionForm(true);
            stale.Description = "설명만 바꾸려던 관리자";
            Assert.True((await service.UpdatePositionAsync(first, CancellationToken.None)).IsSuccess);

            ServiceResult conflict = await service.UpdatePositionAsync(stale, CancellationToken.None);

            Assert.False(conflict.IsSuccess);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.DoesNotContain(db.PositionPermissionGrants, grant => grant.PositionId == 1 && grant.IsAllowed);
                Assert.Empty(db.Positions.Single(position => position.Id == 1).Description);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDatabaseWritersCannotSilentlyOverwriteAdministrativeRecordsAsync(bool isPosition)
    {
        using (TestDbContextFactory factory = createFactory())
        using (OrganizationHubDbContext first = factory.CreateDbContext())
        using (OrganizationHubDbContext second = factory.CreateDbContext())
        {
            if (isPosition)
            {
                first.Positions.Single(position => position.Id == 1).Description = "먼저 저장";
                second.Positions.Single(position => position.Id == 1).Description = "충돌한 저장";
            }
            else
            {
                first.UserAccounts.Single(user => user.Id == 1).DisplayName = "먼저 저장";
                second.UserAccounts.Single(user => user.Id == 1).DisplayName = "충돌한 저장";
            }

            await first.SaveChangesAsync(CancellationToken.None);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(CancellationToken.None));
        }
    }

    internal static TestDbContextFactory createFactory()
    {
        TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_UPDATE);
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            OrganizationPosition member = new OrganizationPosition();
            member.Id = 2;
            member.TeamId = 1;
            member.Name = "일반 구성원";
            member.IsActive = true;
            OrganizationPosition administrator = new OrganizationPosition();
            administrator.Id = 3;
            administrator.TeamId = 1;
            administrator.Name = "검증 관리자";
            administrator.IsActive = true;
            administrator.IsSystemAdministrator = true;
            UserAccount user = new UserAccount();
            user.Id = 10;
            user.Username = "administrator";
            user.DisplayName = "관리자";
            user.TeamId = 1;
            user.PositionId = 3;
            user.IsActive = true;
            db.Positions.AddRange(member, administrator);
            db.UserAccounts.Add(user);
            db.SaveChanges();
        }

        return factory;
    }

    internal static UserSession createAdministratorSession(TestDbContextFactory factory)
    {
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            UserAccount user = db.UserAccounts.Include(account => account.Team).Single(account => account.Id == 10);
            AuthService auth = new AuthService(factory, new Pbkdf2PasswordHashService());
            return UserSessionFactory.CreateOrNull(auth.CreateClaimsPrincipal(user))!;
        }
    }

    private static UserAccountFormModel createUserForm()
    {
        UserAccountFormModel form = new UserAccountFormModel();
        form.Id = 1;
        form.Username = "regression";
        form.DisplayName = "검증 사용자";
        form.TeamId = 1;
        form.PositionId = 1;
        form.IsActive = true;
        return form;
    }

    private static PositionFormModel createPositionForm(bool isAllowed)
    {
        PositionFormModel form = new PositionFormModel();
        form.Id = 1;
        form.TeamId = 1;
        form.Name = "검증 직책";
        form.IsActive = true;
        PositionPermissionGrantFormModel grant = new PositionPermissionGrantFormModel();
        grant.PermissionKey = PermissionKeys.CALENDAR_UPDATE;
        grant.IsAllowed = isAllowed;
        grant.ScopeType = EPermissionScopeType.Global;
        form.PermissionGrants.Add(grant);
        return form;
    }
}
