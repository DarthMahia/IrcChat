using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeTrue();
        result.IsReserved.Should().BeFalse();
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeTrue();
        result.ReservedProvider.Should().Be(ExternalAuthProvider.Google);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsCurrentlyUsed.Should().BeTrue();
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeTrue();
    }

    [Fact]
    public async Task GetProviderConfig_ForGoogle_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Google");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Google);
        config.AuthorizationEndpoint.Should().NotBeNullOrEmpty();
        config.ClientId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProviderConfig_ForMicrosoft_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Microsoft");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Microsoft);
    }

    [Fact]
    public async Task GetProviderConfig_ForFacebook_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Facebook");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Facebook);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedUser = await verifyContext.ReservedUsernames.FindAsync(userToForget.Id);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task ForgetUsername_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeTrue();
        result.ReservedProvider.Should().Be(ExternalAuthProvider.Google);
        result.IsCurrentlyUsed.Should().BeFalse();
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeTrue();
        result.IsCurrentlyUsed.Should().BeTrue();
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeFalse();
        result.ReservedProvider.Should().BeNull();
        result.IsCurrentlyUsed.Should().BeTrue();
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("username_taken");
    }

    [Fact]
    public async Task GetProviderConfig_WithValidProvider_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Google");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadAsStringAsync();
        config.Should().NotBeNull();
    }

    [Fact]
    public async Task ForgetUsername_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/oauth/forget-username", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Vérifier que l'utilisateur a été supprimé
        await using var verifyDb = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deleted = await verifyDb.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Username == "forget_me");
        deleted.Should().BeNull();
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
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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

    private class ProviderConfigResponse
    {
        public ExternalAuthProvider Provider { get; set; }
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}