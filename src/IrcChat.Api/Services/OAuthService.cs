using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using IrcChat.Shared.Models;

namespace IrcChat.Api.Services;

[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Static configuration")]
public class OAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<OAuthService> logger)
{
    private static readonly string _bearer = "Bearer";

    public OAuthConfig GetProviderConfig(ExternalAuthProvider provider)
    {
        return provider switch
        {
            ExternalAuthProvider.Google => new OAuthConfig
            {
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo",
                ClientId = configuration["OAuth:Google:ClientId"] ?? "",
                ClientSecret = configuration["OAuth:Google:ClientSecret"] ?? "",
                Scope = "openid email profile"
            },
            ExternalAuthProvider.Facebook => new OAuthConfig
            {
                AuthorizationEndpoint = "https://www.facebook.com/v18.0/dialog/oauth",
                TokenEndpoint = "https://graph.facebook.com/v18.0/oauth/access_token",
                UserInfoEndpoint = "https://graph.facebook.com/me",
                ClientId = configuration["OAuth:Facebook:AppId"] ?? "",
                ClientSecret = configuration["OAuth:Facebook:AppSecret"] ?? "",
                Scope = "email public_profile"
            },
            ExternalAuthProvider.Microsoft => new OAuthConfig
            {
                AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize",
                TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                UserInfoEndpoint = "https://graph.microsoft.com/v1.0/me",
                ClientId = configuration["OAuth:Microsoft:ClientId"] ?? "",
                ClientSecret = configuration["OAuth:Microsoft:ClientSecret"] ?? "",
                Scope = "openid email profile User.Read"
            },
            _ => throw new ArgumentException($"Provider {provider} not supported")
        };
    }

    public virtual async Task<OAuthTokenResponse?> ExchangeCodeForTokenAsync(
        ExternalAuthProvider provider,
        string code,
        string redirectUri,
        string codeVerifier)
    {
        var config = GetProviderConfig(provider);

        var parameters = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "client_id", config.ClientId },
            { "code_verifier", codeVerifier }, // AJOUT du code_verifier                                               
            { "client_secret", config.ClientSecret } // Ajouter client_secret 
        };

        try
        {
            var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync(config.TokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Token exchange failed for {Provider}: {Error}", provider, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString() ?? "",
                RefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                IdToken = tokenData.TryGetProperty("id_token", out var it) ? it.GetString() : null,
                ExpiresIn = tokenData.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600,
                TokenType = tokenData.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? _bearer : _bearer
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exchanging code for token with {Provider}", provider);
            return null;
        }
        finally
        {
            // Nettoyer le header d'autorisation
            httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public virtual async Task<ExternalUserInfo?> GetUserInfoAsync(
        ExternalAuthProvider provider,
        string accessToken)
    {
        try
        {
            return provider switch
            {
                ExternalAuthProvider.Google => await GetGoogleUserInfo(accessToken),
                ExternalAuthProvider.Facebook => await GetFacebookUserInfo(accessToken),
                ExternalAuthProvider.Microsoft => await GetMicrosoftUserInfo(accessToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user info from {Provider}", provider);
            return null;
        }
    }

    private async Task<ExternalUserInfo?> GetGoogleUserInfo(string accessToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.GetProperty("email").GetString() ?? "",
            Name = data.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl = data.TryGetProperty("picture", out var picture) ? picture.GetString() : null
        };
    }

    private async Task<ExternalUserInfo?> GetFacebookUserInfo(string accessToken)
    {
        var response = await httpClient.GetAsync(
            $"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={accessToken}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
            Name = data.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl = data.TryGetProperty("picture", out var pic)
                ? pic.GetProperty("data").GetProperty("url").GetString()
                : null
        };
    }

    private async Task<ExternalUserInfo?> GetMicrosoftUserInfo(string accessToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(_bearer, accessToken);

        var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.TryGetProperty("mail", out var mail)
                ? mail.GetString() ?? ""
                : data.GetProperty("userPrincipalName").GetString() ?? "",
            Name = data.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null,
            AvatarUrl = null
        };
    }
}

public class ExternalUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}