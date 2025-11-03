// tests/IrcChat.Api.Tests/Integration/ChannelDeleteEndpointsTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
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

public class ChannelDeleteEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task DeleteChannel_AsCreator_ShouldDeleteChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-creator-123",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-delete-channel",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(creator);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifScope = _factory.Services.CreateScope();
        using var verifyContext = verifScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == channel.Name);
        deletedChannel.Should().BeNull();
    }

    [Fact]
    public async Task DeleteChannel_AsAdmin_ShouldDeleteChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-admin-456",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-admin-delete",
            CreatedBy = "someone_else",
            CreatedAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(admin);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verifScope = _factory.Services.CreateScope();
        using var verifyContext = verifScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == channel.Name);
        deletedChannel.Should().BeNull();
    }

    [Fact]
    public async Task DeleteChannel_AsRegularUser_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regular_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-regular-789",
            Email = "regular@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-forbidden-delete",
            CreatedBy = "other_user",
            CreatedAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(user);
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var verifScope = _factory.Services.CreateScope();
        using var verifyContext = verifScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var stillExists = await verifyContext.Channels
            .AnyAsync(c => c.Name == channel.Name);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteChannel_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-unauth-delete",
            CreatedBy = "someone",
            CreatedAt = DateTime.UtcNow
        };

        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteChannel_NonExistent_ShouldReturnNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-test-999",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/channels/non-existent-channel");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteChannel_ShouldSoftDeleteMessages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator_msg",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-creator-msg",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-msg-delete",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Content = "Test message",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.ReservedUsernames.Add(creator);
        db.Channels.Add(channel);
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifScope = _factory.Services.CreateScope();
        using var verifyContext = verifScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedMessage = await verifyContext.Messages
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        deletedMessage.Should().NotBeNull();
        deletedMessage!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteChannel_ShouldRemoveConnectedUsers()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator_users",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-creator-users",
            Email = "creator@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-users-delete",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "connected_user",
            Channel = channel.Name,
            ConnectionId = "conn_123",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ReservedUsernames.Add(creator);
        db.Channels.Add(channel);
        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/channels/{channel.Name}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifScope = _factory.Services.CreateScope();
        using var verifyContext = verifScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var remainingUsers = await verifyContext.ConnectedUsers
            .Where(u => u.Channel == channel.Name)
            .ToListAsync();
        remainingUsers.Should().BeEmpty();
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
}