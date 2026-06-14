using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Services;

public sealed class GoogleApiCredentialFactory : IGoogleApiCredentialFactory
{
    private static readonly TimeSpan CREDENTIAL_CACHE_DURATION = TimeSpan.FromMinutes(45);

    private readonly GoogleApiOptions mOptions;
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IDataProtector mRefreshTokenProtector;
    private readonly IMemoryCache mMemoryCache;

    public GoogleApiCredentialFactory(
        IOptions<GoogleApiOptions> options,
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider,
        IMemoryCache memoryCache
    )
    {
        mOptions = options.Value;
        mDbContextFactory = dbContextFactory;
        mRefreshTokenProtector = dataProtectionProvider.CreateProtector(GoogleTokenProtectorNames.GOOGLE_REFRESH_TOKEN);
        mMemoryCache = memoryCache;
    }

    public async Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
    {
        GoogleStorageConnection? connectionOrNull = await getActiveConnectionOrNullAsync(cancellationToken);
        if (connectionOrNull != null)
        {
            return getCachedUserCredential(connectionOrNull, scopes);
        }

        throw new InvalidOperationException("Google 계정 연결이 필요합니다. 관리자 Google 연결 화면에서 조직 운영용 Google 계정을 연결하세요.");
    }

    private async Task<GoogleStorageConnection?> getActiveConnectionOrNullAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GoogleStorageConnection? connectionOrNull = await dbContext.GoogleStorageConnections
                .Where(connection => connection.IsActive)
                .OrderByDescending(connection => connection.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return connectionOrNull;
        }
    }

    private UserCredential getCachedUserCredential(GoogleStorageConnection connection, string[] scopes)
    {
        string cacheKey = createCredentialCacheKey(connection, scopes);
        UserCredential? credentialOrNull = mMemoryCache.Get<UserCredential>(cacheKey);
        if (credentialOrNull != null)
        {
            return credentialOrNull;
        }

        UserCredential credential = createUserCredential(connection, scopes);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = CREDENTIAL_CACHE_DURATION;
        mMemoryCache.Set(cacheKey, credential, cacheEntryOptions);
        return credential;
    }

    private UserCredential createUserCredential(GoogleStorageConnection connection, string[] scopes)
    {
        TokenResponse tokenResponse = new TokenResponse();
        tokenResponse.RefreshToken = mRefreshTokenProtector.Unprotect(connection.EncryptedRefreshToken);
        GoogleAuthorizationCodeFlow flow = createAuthorizationCodeFlow(scopes);
        return new UserCredential(flow, connection.ConnectedEmail, tokenResponse);
    }

    private string createCredentialCacheKey(GoogleStorageConnection connection, string[] scopes)
    {
        string scopeKey = string.Join("|", scopes.OrderBy(scope => scope));
        List<string> cacheKeyParts = new List<string>
        {
            "OrganizationHub",
            "GoogleCredential",
            connection.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            connection.UpdatedAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            scopeKey
        };
        return string.Join(":", cacheKeyParts);
    }

    private GoogleAuthorizationCodeFlow createAuthorizationCodeFlow(string[] scopes)
    {
        if (string.IsNullOrWhiteSpace(mOptions.OAuthClientId) || string.IsNullOrWhiteSpace(mOptions.OAuthClientSecret))
        {
            throw new InvalidOperationException("Google OAuth client settings are required.");
        }

        ClientSecrets clientSecrets = new ClientSecrets();
        clientSecrets.ClientId = mOptions.OAuthClientId;
        clientSecrets.ClientSecret = mOptions.OAuthClientSecret;

        GoogleAuthorizationCodeFlow.Initializer initializer = new GoogleAuthorizationCodeFlow.Initializer();
        initializer.ClientSecrets = clientSecrets;
        initializer.Scopes = scopes;
        return new GoogleAuthorizationCodeFlow(initializer);
    }
}
