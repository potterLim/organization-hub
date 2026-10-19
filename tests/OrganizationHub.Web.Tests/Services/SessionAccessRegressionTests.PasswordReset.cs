using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class SessionAccessRegressionTests
{
    [Fact]
    public async Task PasswordResetRevokesExistingCookiePrincipalAndCircuitAsync()
    {
        using (TestDbContextFactory factory = AdministrationConcurrencyRegressionTests.createFactory())
        {
            Pbkdf2PasswordHashService hashes = new Pbkdf2PasswordHashService();
            AuthService auth = new AuthService(factory, hashes);
            ClaimsPrincipal principal;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                principal = auth.CreateClaimsPrincipal(db.UserAccounts.Single(user => user.Id == 1));
            }

            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            UserManagementService users = new UserManagementService(factory, hashes);
            ServiceResult reset = await users.ResetPasswordAsync(AdministrationConcurrencyRegressionTests.createAdministratorSession(factory), UserAccountId.CreateExisting(1), PlainPassword.CreateFromInput("Reset-New-2026!"), CancellationToken.None);
            Assert.True(reset.IsSuccess);
            Assert.Null(await access.GetCurrentPrincipalOrNullAsync(principal, CancellationToken.None));
            TestNavigationManager navigation = new TestNavigationManager();
            SessionAccessCircuitHandler circuit = new SessionAccessCircuitHandler(new StaleAuthenticationStateProvider(principal), navigation, access);
            Assert.False(await circuit.CheckAccessAsync(CancellationToken.None));
            Assert.Equal("/login?reason=session-ended", navigation.Destination);
        }
    }

    [Fact]
    public async Task PasswordResetAllowsNewLoginButRejectsOldPasswordAndSessionAsync()
    {
        using (TestDbContextFactory factory = AdministrationConcurrencyRegressionTests.createFactory())
        {
            Pbkdf2PasswordHashService hashes = new Pbkdf2PasswordHashService();
            AuthService auth = new AuthService(factory, hashes);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.UserAccounts.Single(user => user.Id == 1).PasswordHash = hashes.HashPassword(PlainPassword.CreateFromInput("Reset-Old-2026!")).GetRequiredValue();
                await db.SaveChangesAsync();
            }

            LoginResult oldLogin = await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Reset-Old-2026!"), CancellationToken.None);
            Assert.True(oldLogin.IsSuccess);
            UserManagementService users = new UserManagementService(factory, hashes);
            Assert.True((await users.ResetPasswordAsync(AdministrationConcurrencyRegressionTests.createAdministratorSession(factory), UserAccountId.CreateExisting(1), PlainPassword.CreateFromInput("Reset-New-2026!"), CancellationToken.None)).IsSuccess);
            Assert.False((await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Reset-Old-2026!"), CancellationToken.None)).IsSuccess);
            LoginResult newLogin = await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Reset-New-2026!"), CancellationToken.None);
            Assert.True(newLogin.IsSuccess);
            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            Assert.Null(await access.GetCurrentPrincipalOrNullAsync(oldLogin.PrincipalOrNull!, CancellationToken.None));
            Assert.NotNull(await access.GetCurrentPrincipalOrNullAsync(newLogin.PrincipalOrNull!, CancellationToken.None));
        }
    }

    [Fact]
    public async Task PasswordResetDoesNotInvalidateAnotherAccountAsync()
    {
        using (TestDbContextFactory factory = AdministrationConcurrencyRegressionTests.createFactory())
        {
            Pbkdf2PasswordHashService hashes = new Pbkdf2PasswordHashService();
            AuthService auth = new AuthService(factory, hashes);
            ClaimsPrincipal administrator;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                administrator = auth.CreateClaimsPrincipal(db.UserAccounts.Single(user => user.Id == 10));
            }

            UserManagementService users = new UserManagementService(factory, hashes);
            Assert.True((await users.ResetPasswordAsync(AdministrationConcurrencyRegressionTests.createAdministratorSession(factory), UserAccountId.CreateExisting(1), PlainPassword.CreateFromInput("Reset-New-2026!"), CancellationToken.None)).IsSuccess);
            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            Assert.NotNull(await access.GetCurrentPrincipalOrNullAsync(administrator, CancellationToken.None));
        }
    }
}
