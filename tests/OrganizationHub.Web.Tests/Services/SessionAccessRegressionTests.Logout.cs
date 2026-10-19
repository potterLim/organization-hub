using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrganizationHub.Web.Controllers;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class SessionAccessRegressionTests
{
    private sealed class LogoutAuthentication : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? schemeOrNull) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? schemeOrNull, AuthenticationProperties? propertiesOrNull) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? schemeOrNull, AuthenticationProperties? propertiesOrNull) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? schemeOrNull, ClaimsPrincipal principal, AuthenticationProperties? propertiesOrNull) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? schemeOrNull, AuthenticationProperties? propertiesOrNull) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutRejectsExistingCircuitAndCookieIncludingLegacyPrincipalsAsync(bool isLegacy)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            AuthService auth = prepareLogoutAuth(factory);
            LoginResult login = await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Logout-Local-2026!"), CancellationToken.None);
            Assert.True(login.IsSuccess, login.ErrorMessage);
            ClaimsPrincipal principal = login.PrincipalOrNull!;
            if (isLegacy)
            {
                using (OrganizationHubDbContext db = factory.CreateDbContext())
                {
                    principal = auth.CreateClaimsPrincipal(db.UserAccounts.Single());
                }
            }

            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            ClaimsPrincipal refreshed = (await access.GetCurrentPrincipalOrNullAsync(principal, CancellationToken.None))!;
            await logoutAsync(auth, principal);
            Assert.Null(await access.GetCurrentPrincipalOrNullAsync(refreshed, CancellationToken.None));
            Assert.Equal(ESessionAccessState.SignedOut, (await access.ValidateAsync(principal, NavigationPath.CreateOptional("/admin/settings"), CancellationToken.None)).State);
            await logoutAsync(auth, principal);
        }
    }

    [Fact]
    public async Task LogoutOfOneLoginDoesNotRevokeAnotherLoginOfSameAccountAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            AuthService auth = prepareLogoutAuth(factory);
            LoginResult first = await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Logout-Local-2026!"), CancellationToken.None);
            LoginResult second = await auth.ValidateLoginAsync(Username.CreateOptional("regression"), PlainPassword.CreateFromInput("Logout-Local-2026!"), CancellationToken.None);
            await logoutAsync(auth, first.PrincipalOrNull!);
            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            Assert.Null(await access.GetCurrentPrincipalOrNullAsync(first.PrincipalOrNull!, CancellationToken.None));
            Assert.NotNull(await access.GetCurrentPrincipalOrNullAsync(second.PrincipalOrNull!, CancellationToken.None));
        }
    }

    private static AuthService prepareLogoutAuth(TestDbContextFactory factory)
    {
        Pbkdf2PasswordHashService hashes = new Pbkdf2PasswordHashService();
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            db.UserAccounts.Single().PasswordHash = hashes.HashPassword(PlainPassword.CreateFromInput("Logout-Local-2026!")).GetRequiredValue();
            db.SaveChanges();
        }
        return new AuthService(factory, hashes);
    }

    private static async Task logoutAsync(AuthService auth, ClaimsPrincipal principal)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, LogoutAuthentication>();
        using (ServiceProvider provider = services.BuildServiceProvider())
        {
            DefaultHttpContext http = new DefaultHttpContext();
            http.RequestServices = provider;
            http.User = principal;
            AccountController controller = new AccountController(auth);
            ControllerContext context = new ControllerContext();
            context.HttpContext = http;
            controller.ControllerContext = context;
            await controller.LogoutAsync();
        }
    }
}
