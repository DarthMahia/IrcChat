using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
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
    public async Task ToggleMute_AsChannelCreator_ShouldToggleMute()
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MuteResponse>();
        result.Should().NotBeNull();
        result!.IsMuted.Should().BeTrue();
        result.ChannelName.Should().Be(channel.Name);
    }

    [Fact]
    public async Task ToggleMute_AsAdmin_ShouldToggleMute()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

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
            CreatedBy = "someone_else",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        db.ReservedUsernames.Add(admin);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/channels/{channel.Name}/toggle-mute",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MuteResponse>();
        result.Should().NotBeNull();
        result!.IsMuted.Should().BeTrue();
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
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleMute_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/channels/some-channel/toggle-mute",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private class MuteResponse
    {
        public string ChannelName { get; set; } = string.Empty;
        public bool IsMuted { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
    }
}