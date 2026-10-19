using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class SessionAccessRegressionTests
{
    private sealed class StaleAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal mPrincipal;

        public StaleAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            mPrincipal = principal;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(mPrincipal));
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public string Destination { get; private set; } = string.Empty;

        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/admin/settings");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Destination = uri;
        }
    }

    [Fact]
    public async Task DisconnectedCircuitDoesNotAttemptNavigationAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            AuthService auth = new AuthService(factory, new Pbkdf2PasswordHashService());
            ClaimsPrincipal principal;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                principal = auth.CreateClaimsPrincipal(db.UserAccounts.Single());
            }

            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            TestNavigationManager navigation = new TestNavigationManager();
            SessionAccessCircuitHandler handler = new SessionAccessCircuitHandler(new StaleAuthenticationStateProvider(principal), navigation, access);
            await handler.OnConnectionDownAsync(null!, CancellationToken.None);
            Assert.False(await handler.CheckAccessAsync(CancellationToken.None));
            Assert.Empty(navigation.Destination);
            await handler.OnConnectionUpAsync(null!, CancellationToken.None);
            Assert.False(await handler.CheckAccessAsync(CancellationToken.None));
            Assert.Equal("/access-denied", navigation.Destination);
        }
    }

    [Theory]
    [InlineData("user", ESessionAccessState.SignedOut)]
    [InlineData("position", ESessionAccessState.SignedOut)]
    [InlineData("team", ESessionAccessState.SignedOut)]
    [InlineData("permission", ESessionAccessState.Forbidden)]
    public async Task ExistingCircuitAndSettingsWritesAreBlockedAfterRevocationAsync(string change, ESessionAccessState expected)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            AuthService auth = new AuthService(factory, new Pbkdf2PasswordHashService());
            ClaimsPrincipal principal;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                NavigationMenuSection section = new NavigationMenuSection();
                section.DisplayName = "관리";
                db.NavigationMenuSections.Add(section);
                NavigationMenuItem menu = new NavigationMenuItem();
                menu.MenuKey = "AdminSettings";
                menu.DisplayName = "설정";
                menu.Href = "/admin/settings";
                menu.Section = section;
                menu.RequiredPermissionKeys = PermissionKeys.SYSTEM_SETTINGS_MANAGE;
                menu.IsActive = true;
                menu.IsVisibleToAllTeams = true;
                menu.AccessLevel = EMenuAccessLevel.Admin;
                db.NavigationMenuItems.Add(menu);
                await db.SaveChangesAsync();
                principal = auth.CreateClaimsPrincipal(db.UserAccounts.Single());
            }

            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            UserSessionAccessService access = new UserSessionAccessService(factory, auth, new NavigationMenuService(factory, permissions));
            TestNavigationManager navigation = new TestNavigationManager();
            SessionAccessCircuitHandler handler = new SessionAccessCircuitHandler(new StaleAuthenticationStateProvider(principal), navigation, access);
            Assert.True(await handler.CheckAccessAsync(CancellationToken.None));
            GlobalSettingService settings = new GlobalSettingService(factory, permissions);
            GlobalSettingFormModel form = await settings.GetGlobalSettingAsync(CancellationToken.None);
            form.ShouldShowTodoAuthor = false;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                switch (change)
                {
                    case "user":
                        db.UserAccounts.Single().IsActive = false;
                        break;
                    case "position":
                        db.Positions.Single().IsActive = false;
                        break;
                    case "team":
                        db.Teams.Single().IsActive = false;
                        break;
                    case "permission":
                        db.PositionPermissionGrants.RemoveRange(db.PositionPermissionGrants);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(change));
                }

                await db.SaveChangesAsync();
            }

            SessionAccessResult result = await access.ValidateAsync(principal, NavigationPath.CreateOptional("/admin/settings"), CancellationToken.None);
            Assert.Equal(expected, result.State);
            Assert.False(await handler.CheckAccessAsync(CancellationToken.None));
            Assert.Equal(expected == ESessionAccessState.SignedOut ? "/login" : "/access-denied", navigation.Destination);
            ServiceResult saved = await settings.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None);
            Assert.False(saved.IsSuccess);
            Assert.True((await settings.GetGlobalSettingAsync(CancellationToken.None)).ShouldShowTodoAuthor);
            if (expected == ESessionAccessState.SignedOut)
            {
                Assert.Null(await access.GetCurrentPrincipalOrNullAsync(principal, CancellationToken.None));
            }
        }
    }
}
