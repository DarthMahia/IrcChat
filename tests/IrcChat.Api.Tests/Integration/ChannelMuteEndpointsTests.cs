using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class ChannelMuteEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task ToggleMute_AsChannelCreator_ShouldToggleMuteAndBecomeActiveManager()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "channel_creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "creator-123",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-mute-channel",
            CreatedBy = creator.Username,
            ActiveManager = creator.Username,
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        db.ReservedUsernames.Add(creator);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MuteResponse>();
        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
        Assert.Equal(channel.Name, result.ChannelName);

        // Vérifier que le créateur reste le manager actif
        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedChannel = await verifyContext.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal(creator.Username, updatedChannel!.ActiveManager);
        Assert.Equal(creator.Username, updatedChannel.CreatedBy);
    }

    [Fact]
    public async Task ToggleMute_AsAdmin_ShouldUnmuteAndBecomeActiveManager()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "original_creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "creator-456",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_mute",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-mute-123",
            Email = "admin_mute@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "admin-mute-channel",
            CreatedBy = creator.Username,
            ActiveManager = creator.Username,
            CreatedAt = DateTime.UtcNow,
            IsMuted = true // Commence muté
        };

        db.ReservedUsernames.AddRange(creator, admin);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - L'admin démute le salon
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MuteResponse>();
        Assert.NotNull(result);
        Assert.False(result!.IsMuted);

        // Vérifier que l'admin devient le manager actif mais le créateur garde sa propriété
        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedChannel = await verifyContext.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal(admin.Username, updatedChannel!.ActiveManager);
        Assert.Equal(creator.Username, updatedChannel.CreatedBy); // Le créateur ne change pas
    }

    [Fact]
    public async Task ToggleMute_CreatorTakesBackControl_ShouldBecomeActiveManagerAgain()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "original_creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "creator-789",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-789",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "takeback-channel",
            CreatedBy = creator.Username,
            ActiveManager = admin.Username, // Un admin gère actuellement
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };

        db.ReservedUsernames.AddRange(creator, admin);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Le créateur démute son salon
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier que le créateur redevient le manager actif
        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedChannel = await verifyContext.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal(creator.Username, updatedChannel!.ActiveManager);
        Assert.Equal(creator.Username, updatedChannel.CreatedBy);
    }

    [Fact]
    public async Task ToggleMute_MuteDoesNotChangeActiveManager()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator_mute",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "creator-mute-123",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "mute-no-change",
            CreatedBy = creator.Username,
            ActiveManager = creator.Username,
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        db.ReservedUsernames.Add(creator);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Muter le salon (pas démuter)
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier que le manager actif n'a PAS changé lors du mute
        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedChannel = await verifyContext.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.True(updatedChannel!.IsMuted);
        Assert.Equal(creator.Username, updatedChannel.ActiveManager); // Reste inchangé
    }

    [Fact]
    public async Task ToggleMute_AsRegularUser_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "channel_owner",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "owner-123",
            Email = "owner@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regular_user_mute",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "regular-mute-456",
            Email = "regular_mute@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "forbidden-mute-channel",
            CreatedBy = creator.Username,
            ActiveManager = creator.Username,
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        db.ReservedUsernames.AddRange(creator, regularUser);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(regularUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ToggleMute_ChannelNotFound_ShouldReturnNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "test_user_notfound",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "notfound-123",
            Email = "notfound@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            "/api/channels/non-existent-channel/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ToggleMute_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/channels/some-channel/toggle-mute",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SuppressMessage("Blocker Vulnerability", "S6781:JWT secret keys should not be disclosed", Justification = "Not revelant in test")]
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

    private sealed class MuteResponse
    {
        public string ChannelName { get; init; } = string.Empty;
        public bool IsMuted { get; init; } = false;
        public string ChangedBy { get; init; } = string.Empty;
    }
}