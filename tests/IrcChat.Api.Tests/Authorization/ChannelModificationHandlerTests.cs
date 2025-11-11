using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Authorization;

public class ChannelModificationHandlerTests
{
    private readonly ChatDbContext _db;
    private readonly Mock<ILogger<ChannelModificationHandler>> _loggerMock;
    private readonly ChannelModificationHandler _handler;

    public ChannelModificationHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _db = new ChatDbContext(options);
        _loggerMock = new Mock<ILogger<ChannelModificationHandler>>();
        _handler = new ChannelModificationHandler(_db, _loggerMock.Object);
    }

    [Fact]
    public async Task Handler_WithNoUsername_ShouldFail()
    {
        // Arrange
        var requirement = new ChannelModificationRequirement("test-channel");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Handler_WithNonExistentChannel_ShouldSucceed()
    {
        // Arrange - Un canal inexistant doit réussir l'autorisation
        // C'est à l'endpoint de retourner NotFound après
        var requirement = new ChannelModificationRequirement("non-existent");
        var claims = new List<Claim> { new(ClaimTypes.Name, "testuser") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded); // Doit réussir pour permettre NotFound dans l'endpoint
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Handler_WithCreator_ShouldSucceed()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "creator") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Handler_WithAdmin_ShouldSucceed()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin123",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        _db.ReservedUsernames.Add(admin);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Handler_WithRegularUser_ShouldFail()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regular",
            Email = "regular@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "regular123",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        _db.ReservedUsernames.Add(regularUser);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "regular") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Handler_WithCaseInsensitiveChannelName_ShouldWork()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "Test-Channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "creator") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_WithCaseInsensitiveUsername_ShouldWork()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "Creator",
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "creator") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_ShouldLogWarningWhenNoUsername()
    {
        // Arrange
        var requirement = new ChannelModificationRequirement("test-channel");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("sans username")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_ShouldLogInformationWhenAuthorized()
    {
        // Arrange
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(channel);
        await _db.SaveChangesAsync();

        var requirement = new ChannelModificationRequirement("test-channel");
        var claims = new List<Claim> { new(ClaimTypes.Name, "creator") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("autorisé")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}