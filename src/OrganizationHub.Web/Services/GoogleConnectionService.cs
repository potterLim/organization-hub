using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Drive.v3;
using Google.Apis.Tasks.v1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class GoogleConnectionService : IGoogleConnectionService
{
    private static readonly string[] OAUTH_SCOPES = new[]
    {
        DriveService.Scope.Drive,
        CalendarService.Scope.Calendar,
        TasksService.Scope.Tasks,
        "openid",
        "email"
    };

    private readonly GoogleApiOptions mOptions;
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IDataProtector mRefreshTokenProtector;
    private readonly IDataProtector mStateProtector;

    public GoogleConnectionService(
        IOptions<GoogleApiOptions> options,
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider
    )
    {
        mOptions = options.Value;
        mDbContextFactory = dbContextFactory;
        mRefreshTokenProtector = dataProtectionProvider.CreateProtector(GoogleTokenProtectorNames.GOOGLE_REFRESH_TOKEN);
        mStateProtector = dataProtectionProvider.CreateProtector(GoogleTokenProtectorNames.GOOGLE_OAUTH_STATE);
    }

    public async Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        GoogleConnectionStatus status = new GoogleConnectionStatus();
        status.HasOAuthConfiguration = hasOAuthConfiguration();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GoogleStorageConnection? connectionOrNull = await dbContext.GoogleStorageConnections.Where(connection => connection.IsActive).OrderByDescending(connection => connection.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
            if (connectionOrNull == null)
            {
                status.Message = status.HasOAuthConfiguration ? "Google 계정이 아직 연결되지 않았습니다." : "Google OAuth Client ID와 Client Secret이 설정되지 않았습니다.";
                return status;
            }

            status.IsConnected = true;
            status.ConnectedEmail = connectionOrNull.ConnectedEmail;
            status.ConnectedAt = connectionOrNull.ConnectedAt;
            status.LastValidatedAt = connectionOrNull.LastValidatedAt;
            status.Message = "Google 계정이 연결되어 있습니다.";
            return status;
        }
    }

    public string CreateAuthorizationUrl(GoogleOAuthRedirectUri redirectUri)
    {
        if (hasOAuthConfiguration() == false)
        {
            throw new InvalidOperationException("Google OAuth client settings are required.");
        }

        GoogleAuthorizationCodeFlow flow = createAuthorizationCodeFlow(OAUTH_SCOPES);
        AuthorizationCodeRequestUrl request = flow.CreateAuthorizationCodeRequest(redirectUri.Value);
        GoogleAuthorizationCodeRequestUrl googleRequest = (GoogleAuthorizationCodeRequestUrl)request;
        googleRequest.AccessType = "offline";
        googleRequest.Prompt = "consent";
        googleRequest.State = createProtectedState();
        return googleRequest.Build().ToString();
    }

    public async Task<ServiceResult> CompleteConnectionAsync(
        GoogleOAuthAuthorizationCode authorizationCode,
        GoogleOAuthState state,
        GoogleOAuthRedirectUri redirectUri,
        CancellationToken cancellationToken
    )
    {
        if (hasOAuthConfiguration() == false)
        {
            return ServiceResult.Fail("Google OAuth Client ID와 Client Secret이 설정되지 않았습니다.");
        }

        if (authorizationCode.HasValue == false)
        {
            return ServiceResult.Fail("Google OAuth 인증 코드가 비어 있습니다.");
        }

        if (validateState(state) == false)
        {
            return ServiceResult.Fail("Google OAuth state가 유효하지 않습니다. 다시 연결해 주세요.");
        }

        GoogleAuthorizationCodeFlow flow = createAuthorizationCodeFlow(OAUTH_SCOPES);
        TokenResponse tokenResponse = await flow.ExchangeCodeForTokenAsync("organization-hub-storage", authorizationCode.Value, redirectUri.Value, cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            return ServiceResult.Fail("Google refresh token을 받지 못했습니다. Google 권한 연결을 해제한 뒤 다시 연결해 주세요.");
        }

        string connectedEmail = await getConnectedEmailAsync(tokenResponse);
        await saveConnectionAsync(connectedEmail, tokenResponse.RefreshToken, cancellationToken);
        return ServiceResult.Succeed();
    }

    public async Task<ServiceResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<GoogleStorageConnection> connections = await dbContext.GoogleStorageConnections.Where(connection => connection.IsActive).ToListAsync(cancellationToken);
            foreach (GoogleStorageConnection connection in connections)
            {
                connection.IsActive = false;
                connection.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private bool hasOAuthConfiguration()
    {
        return string.IsNullOrWhiteSpace(mOptions.OAuthClientId) == false && string.IsNullOrWhiteSpace(mOptions.OAuthClientSecret) == false;
    }

    private GoogleAuthorizationCodeFlow createAuthorizationCodeFlow(string[] scopes)
    {
        ClientSecrets clientSecrets = new ClientSecrets();
        clientSecrets.ClientId = mOptions.OAuthClientId;
        clientSecrets.ClientSecret = mOptions.OAuthClientSecret;

        GoogleAuthorizationCodeFlow.Initializer initializer = new GoogleAuthorizationCodeFlow.Initializer();
        initializer.ClientSecrets = clientSecrets;
        initializer.Scopes = scopes;
        return new GoogleAuthorizationCodeFlow(initializer);
    }

    private string createProtectedState()
    {
        string statePayload = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N");
        return mStateProtector.Protect(statePayload);
    }

    private bool validateState(GoogleOAuthState state)
    {
        if (state.HasValue == false)
        {
            return false;
        }

        try
        {
            string statePayload = mStateProtector.Unprotect(state.Value);
            string[] parts = statePayload.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            long createdUnixSeconds;
            bool isParsed = long.TryParse(parts[0], out createdUnixSeconds);
            if (isParsed == false)
            {
                return false;
            }

            DateTimeOffset createdAt = DateTimeOffset.FromUnixTimeSeconds(createdUnixSeconds);
            return DateTimeOffset.UtcNow - createdAt < TimeSpan.FromMinutes(15);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<string> getConnectedEmailAsync(TokenResponse tokenResponse)
    {
        if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            throw new InvalidOperationException("Google ID token is required to identify the connected account.");
        }

        GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(tokenResponse.IdToken);
        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new InvalidOperationException("Google account email is required.");
        }

        return payload.Email;
    }

    private async Task saveConnectionAsync(string connectedEmail, string refreshToken, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string encryptedRefreshToken = mRefreshTokenProtector.Protect(refreshToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<GoogleStorageConnection> existingConnections = await dbContext.GoogleStorageConnections.ToListAsync(cancellationToken);
            foreach (GoogleStorageConnection connection in existingConnections)
            {
                connection.IsActive = false;
                connection.UpdatedAt = now;
            }

            GoogleStorageConnection storageConnection = new GoogleStorageConnection();
            storageConnection.ConnectedEmail = connectedEmail;
            storageConnection.EncryptedRefreshToken = encryptedRefreshToken;
            storageConnection.Scopes = string.Join(" ", OAUTH_SCOPES);
            storageConnection.IsActive = true;
            storageConnection.ConnectedAt = now;
            storageConnection.LastValidatedAt = now;
            storageConnection.UpdatedAt = now;
            dbContext.GoogleStorageConnections.Add(storageConnection);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
