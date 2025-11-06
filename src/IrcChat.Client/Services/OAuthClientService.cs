using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IrcChat.Shared.Models;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class OAuthClientService(IJSRuntime jsRuntime, HttpClient httpClient) : IOAuthClientService
{
    public async Task<string> InitiateAuthorizationFlowAsync(ExternalAuthProvider provider, string redirectUri)
    {
        var response = await httpClient.GetFromJsonAsync<OAuthProviderConfig>(
            $"/api/oauth/config/{provider}") ?? throw new InvalidOperationException("Failed to get OAuth configuration");
        var state = GenerateRandomString(32);
        var codeVerifier = GenerateRandomString(128);
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_state", state);
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_code_verifier", codeVerifier);
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_provider", provider.ToString());

        var authUrl = $"{response.AuthorizationEndpoint}" +
            $"?client_id={Uri.EscapeDataString(response.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(response.Scope)}" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        return authUrl;
    }

    public async Task<OAuthLoginResponse?> HandleCallbackAsync(string code, string state, string redirectUri)
    {
        var savedState = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_state");
        if (state != savedState)
        {
            throw new InvalidOperationException("Invalid state parameter - possible CSRF attack");
        }

        var providerStr = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_provider");
        var codeVerifier = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_code_verifier");

        if (!Enum.TryParse<ExternalAuthProvider>(providerStr, out var provider))
        {
            throw new InvalidOperationException("Invalid provider");
        }

        var tokenRequest = new OAuthTokenRequest
        {
            Provider = provider,
            Code = code,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier
        };

        var response = await httpClient.PostAsJsonAsync("/api/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Token exchange failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();

        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_state");
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_code_verifier");
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_provider");

        return result;
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        return new string([.. random.Select(b => chars[b % chars.Length])]);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class OAuthProviderConfig
{
    public ExternalAuthProvider Provider { get; set; }
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}