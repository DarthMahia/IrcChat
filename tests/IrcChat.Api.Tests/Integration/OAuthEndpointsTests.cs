using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class OAuthEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task CheckUsername_WithAvailableUsername_ShouldReturnAvailable()
    {
        // Arrange
        var request = new UsernameCheckRequest
        {
            Username = "available_user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Available);
        Assert.False(result.IsReserved);
    }

    [Fact]
    public async Task CheckUsername_WithReservedUsername_ShouldReturnNotAvailable()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "reserved_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "reserved@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest
        {
            Username = "reserved_user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.True(result.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, result.ReservedProvider);
    }

    [Fact]
    public async Task CheckUsername_WithConnectedUser_ShouldReturnNotAvailable()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "connected_user",
            Channel = "general",
            ConnectionId = "conn-123",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest
        {
            Username = "connected_user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.True(result.IsCurrentlyUsed);
    }

    [Fact]
    public async Task CheckUsername_CaseInsensitive_ShouldWork()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "CaseSensitive",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-case-123",
            Email = "case@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest
        {
            Username = "casesensitive" // lowercase
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.True(result.IsReserved);
    }

    [Fact]
    public async Task GetProviderConfig_ForGoogle_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Google");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        Assert.NotNull(config);
        Assert.Equal(ExternalAuthProvider.Google, config!.Provider);
        Assert.NotEmpty(config.AuthorizationEndpoint);
        Assert.NotEmpty(config.ClientId);
    }

    [Fact]
    public async Task GetProviderConfig_ForMicrosoft_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Microsoft");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        Assert.NotNull(config);
        Assert.Equal(ExternalAuthProvider.Microsoft, config!.Provider);
    }

    [Fact]
    public async Task GetProviderConfig_ForFacebook_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Facebook");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        Assert.NotNull(config);
        Assert.Equal(ExternalAuthProvider.Facebook, config!.Provider);
    }

    [Fact]
    public async Task ForgetUsername_WithAuth_ShouldDeleteReservedUser()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var userToForget = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "forget_me",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "forget-123",
            Email = "forget@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(userToForget);
        await db.SaveChangesAsync();

        var token = GenerateToken(userToForget);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedUser = await verifyContext.ReservedUsernames.FindAsync(userToForget.Id);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task ForgetUsername_NonExistentUser_ShouldReturnNotFound()
    {
        // Arrange
        var fakeUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "nonexistent",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "fake-123",
            Email = "fake@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var token = GenerateToken(fakeUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckUsername_WithReservedButNotCurrentlyUsed_ShouldReturnCorrectStatus()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "reserved_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "reserved@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest { Username = "reserved_user" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.True(result.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, result.ReservedProvider);
        Assert.False(result.IsCurrentlyUsed);
    }

    [Fact]
    public async Task CheckUsername_WithReservedAndCurrentlyUsed_ShouldReturnBothFlags()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            Provider = ExternalAuthProvider.Microsoft,
            ExternalUserId = "ms-456",
            Email = "active@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            ConnectionId = "conn-123",
            Channel = "general",
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ReservedUsernames.Add(reservedUser);
        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest { Username = "active_user" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.True(result.IsReserved);
        Assert.True(result.IsCurrentlyUsed);
    }

    [Fact]
    public async Task CheckUsername_WithOnlyCurrentlyUsed_ShouldReturnCorrectStatus()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "guest_user",
            ConnectionId = "conn-456",
            Channel = "general",
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest { Username = "guest_user" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.False(result.IsReserved);
        Assert.Null(result.ReservedProvider);
        Assert.True(result.IsCurrentlyUsed);
    }

    [Fact]
    public async Task ReserveUsername_WithExistingReservation_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var existingUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "taken_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-existing",
            Email = "existing@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(existingUser);
        await db.SaveChangesAsync();

        var request = new ReserveUsernameRequest
        {
            Username = "taken_user",
            Provider = ExternalAuthProvider.Facebook,
            Code = "test_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "test_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("username_taken", errorContent);
    }

    [Fact]
    public async Task GetProviderConfig_WithValidProvider_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Google");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadAsStringAsync();
        Assert.NotNull(config);
    }

    [Fact]
    public async Task ForgetUsername_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgetUsername_WithAuthentication_ShouldDeleteReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var userToForget = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "forget_me",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-forget",
            Email = "forget@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(userToForget);
        await db.SaveChangesAsync();

        var token = GenerateToken(userToForget);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier que l'utilisateur a été supprimé
        await using var verifyDb = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deleted = await verifyDb.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Username == "forget_me");
        Assert.Null(deleted);
    }

    [Fact]
    public async Task ForgetUsername_WithNonExistentUser_ShouldReturnNotFound()
    {
        // Arrange
        var fakeUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "nonexistent",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-fake",
            Email = "fake@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var token = GenerateToken(fakeUser);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Tests à ajouter à OAuthEndpointsTests.cs

    [Fact]
    public async Task ReserveUsername_WithValidData_ShouldCreateReservationAndReturnToken()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        // Arrange
        var request = new ReserveUsernameRequest
        {
            Username = "new_user",
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Token);
        Assert.Equal("new_user", result.Username);
        Assert.True(result.IsNewUser);

        // Vérifier que l'utilisateur a été créé en base
        using var scopeVerifier = _factory.Services.CreateScope();
        var dbVerifier = scopeVerifier.ServiceProvider.GetRequiredService<ChatDbContext>();
        var created = await dbVerifier.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Username == "new_user");
        Assert.NotNull(created);
    }

    [Fact]
    public async Task ReserveUsername_FirstUser_ShouldBeAdmin()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        // S'assurer qu'il n'y a pas d'utilisateurs
        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var request = new ReserveUsernameRequest
        {
            Username = "first_admin",
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.True(result!.IsAdmin);
    }

    [Fact]
    public async Task ReserveUsername_SecondUser_ShouldNotBeAdmin()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        // Créer un premier utilisateur
        var firstUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "first_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-first",
            Email = "first@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };
        db.ReservedUsernames.Add(firstUser);
        await db.SaveChangesAsync();

        var request = new ReserveUsernameRequest
        {
            Username = "second_user",
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.False(result!.IsAdmin);
    }

    [Fact]
    public async Task ReserveUsername_WithExistingExternalUserId_ShouldReturnAlreadyReserved()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var existingUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "existing_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "existing@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        db.ReservedUsernames.Add(existingUser);
        await db.SaveChangesAsync();

        var request = new ReserveUsernameRequest
        {
            Username = "different_username",
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code", // Ce code retournera google-123 comme ExternalUserId
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("already_reserved", errorContent);
        Assert.Contains("existing_user", errorContent);
    }

    [Fact]
    public async Task ReserveUsername_WithInvalidCode_ShouldReturnUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        // Arrange
        var request = new ReserveUsernameRequest
        {
            Username = "test_user",
            Provider = ExternalAuthProvider.Google,
            Code = "invalid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "invalid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginReserved_WithValidCredentials_ShouldReturnTokenAndUpdateLastLogin()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var oldLoginTime = DateTime.UtcNow.AddDays(-7);
        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "login_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "login@example.com",
            AvatarUrl = "old_avatar.jpg",
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            LastLoginAt = oldLoginTime,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Token);
        Assert.Equal("login_user", result.Username);
        Assert.False(result.IsNewUser);

        // Vérifier que LastLoginAt a été mis à jour
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updated = await verifyDb.ReservedUsernames.FindAsync(user.Id);
        Assert.True(updated!.LastLoginAt > oldLoginTime);
    }

    [Fact]
    public async Task LoginReserved_WithNonExistentUser_ShouldReturnNotFound()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("not_found", errorContent);
    }

    [Fact]
    public async Task LoginReserved_WithInvalidCode_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "invalid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "invalid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginReserved_ShouldUpdateAvatarUrl()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "avatar_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "avatar@example.com",
            AvatarUrl = "old_avatar.jpg",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updated = await verifyDb.ReservedUsernames.FindAsync(user.Id);
        Assert.NotEqual("old_avatar.jpg", updated!.AvatarUrl);
    }

    [Fact]
    public async Task ReserveUsername_ShouldTrimUsername()
    {
        // Arrange
        var request = new ReserveUsernameRequest
        {
            Username = "  trimmed_user  ",
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.Equal("trimmed_user", result!.Username);
    }

    [SuppressMessage("Blocker Vulnerability", "S6781:JWT secret keys should not be disclosed", Justification = "It's a test")]
    private static string GenerateToken(ReservedUsername user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VotreCleSecrete123456789012345678901234567890"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("provider", user.Provider.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "IrcChatApi",
            audience: "IrcChatClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ProviderConfigResponse
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized")]
        public ExternalAuthProvider Provider { get; set; }
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}