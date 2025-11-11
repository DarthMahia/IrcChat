// tests/IrcChat.Client.Tests/Services/PrivateMessageServiceTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class PrivateMessageServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public PrivateMessageServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost:7000");
    }

    [Fact]
    public void NotifyPrivateMessageReceived_ShouldTriggerEvents()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);
        PrivateMessage? receivedMessage = null;
        var unreadCountChanged = false;

        service.OnPrivateMessageReceived += (msg) => receivedMessage = msg;
        service.OnUnreadCountChanged += () => unreadCountChanged = true;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageReceived(testMessage);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(testMessage.Id, receivedMessage.Id);
        Assert.Equal(testMessage.SenderUsername, receivedMessage.SenderUsername);
        Assert.Equal(testMessage.Content, receivedMessage.Content);
        Assert.True(unreadCountChanged);
    }

    [Fact]
    public void NotifyPrivateMessageSent_ShouldTriggerEvent()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);
        PrivateMessage? sentMessage = null;

        service.OnPrivateMessageSent += (msg) => sentMessage = msg;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageSent(testMessage);

        // Assert
        Assert.NotNull(sentMessage);
        Assert.Equal(testMessage.Id, sentMessage.Id);
        Assert.Equal(testMessage.SenderUsername, sentMessage.SenderUsername);
        Assert.Equal(testMessage.Content, sentMessage.Content);
    }

    [Fact]
    public void NotifyMessagesRead_ShouldTriggerEvent()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);
        string? readUsername = null;
        List<Guid>? readMessageIds = null;

        service.OnMessagesRead += (username, messageIds) =>
        {
            readUsername = username;
            readMessageIds = messageIds;
        };

        var messageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        service.NotifyMessagesRead("testUser", messageIds);

        // Assert
        Assert.Equal("testUser", readUsername);
        Assert.NotNull(readMessageIds);
        Assert.Equal(2, readMessageIds.Count);
        Assert.Equal(messageIds[0], readMessageIds[0]);
        Assert.Equal(messageIds[1], readMessageIds[1]);
    }

    [Fact]
    public async Task GetConversationsAsync_ShouldReturnConversations()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2
            },
            new()
            {
                OtherUsername = "user2",
                LastMessage = "Hi",
                LastMessageTime = DateTime.UtcNow.AddHours(-1),
                UnreadCount = 0
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/conversations/testUser")
            .Respond(HttpStatusCode.OK, JsonContent.Create(conversations));

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetConversationsAsync("testUser");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("user1", result[0].OtherUsername);
        Assert.Equal("Hello", result[0].LastMessage);
        Assert.Equal(2, result[0].UnreadCount);
        Assert.Equal("user2", result[1].OtherUsername);
        Assert.Equal("Hi", result[1].LastMessage);
        Assert.Equal(0, result[1].UnreadCount);
    }

    [Fact]
    public async Task GetConversationsAsync_OnError_ShouldReturnEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/conversations/testUser")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetConversationsAsync("testUser");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ShouldReturnMessages()
    {
        // Arrange
        var messages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                RecipientUsername = "user2",
                Content = "Message 1",
                Timestamp = DateTime.UtcNow,
                IsRead = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user2",
                RecipientUsername = "user1",
                Content = "Message 2",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/user1/with/user2")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetPrivateMessagesAsync("user1", "user2");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("user1", result[0].SenderUsername);
        Assert.Equal("Message 1", result[0].Content);
        Assert.True(result[0].IsRead);
        Assert.Equal("user2", result[1].SenderUsername);
        Assert.Equal("Message 2", result[1].Content);
        Assert.False(result[1].IsRead);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_OnError_ShouldReturnEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/user1/with/user2")
            .Respond(HttpStatusCode.NotFound);

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetPrivateMessagesAsync("user1", "user2");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldReturnCount()
    {
        // Arrange
        var unreadResponse = new { UnreadCount = 5 };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/testUser/unread-count")
            .Respond(HttpStatusCode.OK, JsonContent.Create(unreadResponse));

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetUnreadCountAsync("testUser");

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_OnError_ShouldReturnZero()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/testUser/unread-count")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.GetUnreadCountAsync("testUser");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteConversationAsync_ShouldReturnTrueAndTriggerEvent()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Respond(HttpStatusCode.OK);

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);
        string? deletedUsername = null;
        var unreadCountChanged = false;

        service.OnConversationDeleted += (username) => deletedUsername = username;
        service.OnUnreadCountChanged += () => unreadCountChanged = true;

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        Assert.True(result);
        Assert.Equal("user2", deletedUsername);
        Assert.True(unreadCountChanged);
    }

    [Fact]
    public async Task DeleteConversationAsync_OnError_ShouldReturnFalse()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Respond(HttpStatusCode.NotFound);

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteConversationAsync_OnException_ShouldReturnFalse()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Throw(new HttpRequestException("Network error"));

        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MultipleEventSubscribers_ShouldAllBeNotified()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient, NullLogger<PrivateMessageService>.Instance);
        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnPrivateMessageReceived += _ => subscriber1Called++;
        service.OnPrivateMessageReceived += _ => subscriber2Called++;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageReceived(testMessage);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
    }

    [Fact]
    public async Task GetConversationsAsync_OnException_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PrivateMessageService>>();

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/conversations/testUser")
            .Throw(new HttpRequestException("Network error"));

        var service = new PrivateMessageService(_httpClient, loggerMock.Object);

        // Act
        var result = await service.GetConversationsAsync("testUser");

        // Assert
        Assert.Empty(result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("récupération des conversations")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_OnException_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PrivateMessageService>>();

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/user1/with/user2")
            .Throw(new HttpRequestException("Connection timeout"));

        var service = new PrivateMessageService(_httpClient, loggerMock.Object);

        // Act
        var result = await service.GetPrivateMessagesAsync("user1", "user2");

        // Assert
        Assert.Empty(result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("récupération des messages entre")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUnreadCountAsync_OnException_ShouldLogWarningAndReturnZero()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PrivateMessageService>>();

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/testUser/unread-count")
            .Throw(new TaskCanceledException("Request timeout"));

        var service = new PrivateMessageService(_httpClient, loggerMock.Object);

        // Act
        var result = await service.GetUnreadCountAsync("testUser");

        // Assert
        Assert.Equal(0, result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("récupération du nombre de messages non lus")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_OnException_ShouldLogErrorAndReturnFalse()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PrivateMessageService>>();

        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Throw(new InvalidOperationException("Unexpected error"));

        var service = new PrivateMessageService(_httpClient, loggerMock.Object);

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        Assert.False(result);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("suppression de la conversation entre")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}