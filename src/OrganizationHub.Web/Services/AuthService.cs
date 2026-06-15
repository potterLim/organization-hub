using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPasswordHashService mPasswordHashService;

    public AuthService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPasswordHashService passwordHashService)
    {
        mDbContextFactory = dbContextFactory;
        mPasswordHashService = passwordHashService;
    }

    public async Task<LoginResult> ValidateLoginAsync(Username username, PlainPassword password, CancellationToken cancellationToken)
    {
        if (username.HasValue == false)
        {
            return LoginResult.Fail("아이디와 비밀번호를 확인해 주세요.");
        }

        if (password.HasValue == false)
        {
            return LoginResult.Fail("아이디와 비밀번호를 확인해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string normalizedUsername = username.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Team).FirstOrDefaultAsync(user => user.Username == normalizedUsername, cancellationToken);
            if (userAccountOrNull == null)
            {
                return LoginResult.Fail("아이디와 비밀번호를 확인해 주세요.");
            }

            if (userAccountOrNull.IsActive == false)
            {
                return LoginResult.Fail("비활성화된 계정입니다. 관리자에게 문의해 주세요.");
            }

            PasswordHash storedPasswordHash = PasswordHash.CreateFromStoredValue(userAccountOrNull.PasswordHash);
            bool isPasswordValid = mPasswordHashService.VerifyPassword(password, storedPasswordHash);
            if (isPasswordValid == false)
            {
                return LoginResult.Fail("아이디와 비밀번호를 확인해 주세요.");
            }

            if (UserRoleUtility.IsTeamRole(userAccountOrNull.Role))
            {
                if (userAccountOrNull.Team == null || userAccountOrNull.Team.IsActive == false)
                {
                    return LoginResult.Fail("소속 팀이 비활성화되어 로그인할 수 없습니다.");
                }
            }

            userAccountOrNull.LastLoginAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            ClaimsPrincipal principal = CreateClaimsPrincipal(userAccountOrNull);
            return LoginResult.Succeed(principal);
        }
    }

    public ClaimsPrincipal CreateClaimsPrincipal(UserAccount userAccount)
    {
        List<Claim> claims = new List<Claim>();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, userAccount.Id.ToString()));
        claims.Add(new Claim(ClaimTypes.Name, userAccount.Username));
        claims.Add(new Claim(ClaimTypes.Role, UserRoleUtility.GetRoleName(userAccount.Role)));
        claims.Add(new Claim(OrganizationHubClaimTypes.USER_ID, userAccount.Id.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.USERNAME, userAccount.Username));
        claims.Add(new Claim(OrganizationHubClaimTypes.DISPLAY_NAME, userAccount.DisplayName));
        claims.Add(new Claim(OrganizationHubClaimTypes.ROLE, UserRoleUtility.GetRoleName(userAccount.Role)));

        if (userAccount.TeamId.HasValue)
        {
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_ID, userAccount.TeamId.Value.ToString()));
        }

        if (userAccount.Team != null)
        {
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_KEY, userAccount.Team.TeamKey));
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_DISPLAY_NAME, userAccount.Team.DisplayName));
        }

        ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
