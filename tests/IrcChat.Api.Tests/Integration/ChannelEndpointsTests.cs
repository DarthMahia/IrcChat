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

public class ChannelEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetChannels_ShouldReturnChannelList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var channels = await response.Content.ReadFromJsonAsync<List<Channel>>();
        Assert.NotNull(channels);
    }

    [Fact]
    public async Task GetConnectedUsers_ShouldReturnUsersInChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = "test-channel-users";

        var user1 = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Channel = channel,
            ConnectionId = "conn1",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        var user2 = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Channel = channel,
            ConnectionId = "conn2",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ConnectedUsers.AddRange(user1, user2);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/channels/{channel}/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.NotNull(users);
        Assert.True(users.Count >= 2);
        Assert.Contains(users, u => u.Username == "user1");
        Assert.Contains(users, u => u.Username == "user2");
    }

    [Fact]
    public async Task CreateChannel_WithAuthentication_ShouldCreateChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "oauth_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-test-123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(created);
        Assert.Equal("test-channel", created!.Name);
        Assert.Equal(reservedUser.Username, created.CreatedBy);
    }

    [Fact]
    public async Task CreateChannel_WithEmptyUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("missing_username", error!.Error);
    }

    [Fact]
    public async Task CreateChannel_WithoutAuthentication_ShouldReturnForbidden()
    {
        // Arrange
        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = "testuser"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateChannel_DuplicateName_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "duplicate_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-dup-123",
            Email = "dup@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);

        var existingChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "duplicate-channel",
            CreatedBy = reservedUser.Username,
            CreatedAt = DateTime.UtcNow
        };

        db.Channels.Add(existingChannel);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "duplicate-channel",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("channel_exists", error!.Error);
    }

    [Fact]
    public async Task CreateChannel_ShouldTrimChannelName()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "trim_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-trim-123",
            Email = "trim@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "  trimmed-channel  ",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(created);
        Assert.Equal("trimmed-channel", created!.Name);
    }

    [Fact]
    public async Task GetConnectedUsers_EmptyChannel_ShouldReturnEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels/empty-channel-no-users/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.NotNull(users);
        Assert.Empty(users);
    }

    private static string GenerateOAuthToken(ReservedUsername user)
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

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}