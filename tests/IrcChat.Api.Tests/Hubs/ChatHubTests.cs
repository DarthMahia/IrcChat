using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Hubs;

public class ChatHubTests : IAsyncDisposable
{
    private readonly ChatDbContext _db;
    private readonly Mock<IOptions<ConnectionManagerOptions>> _optionsMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<ISingleClientProxy> _callerMock;
    private readonly Mock<IClientProxy> _allClientsMock;
    private readonly Mock<IClientProxy> _groupMock;
    private readonly Mock<ISingleClientProxy> _singleClientMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly ChatHub _hub;

    public ChatHubTests()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);

        var connectionManagerOptions = new ConnectionManagerOptions
        {
            InstanceId = "test-instance",
            CleanupIntervalSeconds = 60,
            UserTimeoutSeconds = 300
        };
        _optionsMock = new Mock<IOptions<ConnectionManagerOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(connectionManagerOptions);

        _clientsMock = new Mock<IHubCallerClients>();
        _callerMock = new Mock<ISingleClientProxy>();
        _allClientsMock = new Mock<IClientProxy>();
        _groupMock = new Mock<IClientProxy>();
        _singleClientMock = new Mock<ISingleClientProxy>();
        _contextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();

        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupMock.Object);
        _clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_singleClientMock.Object);

        _contextMock.Setup(c => c.ConnectionId).Returns("test-connection-id");

        // Correction: ChatHub prend 2 paramètres (db, options)
        _hub = new ChatHub(_db, _optionsMock.Object)
        {
            Clients = _clientsMock.Object,
            Context = _contextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    [Fact]
    public async Task JoinChannel_ShouldAddUserToChannel()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        // Créer le canal d'abord
        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(username, channel);

        // Assert
        var connectedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.Channel == channel);

        Assert.NotNull(connectedUser);
        Assert.Equal("test-connection-id", connectedUser!.ConnectionId);

        _groupManagerMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", channel, default),
            Times.Once);

        _groupMock.Verify(
            g => g.SendCoreAsync(
                "UserJoined",
                It.Is<object[]>(args => args.Length == 2),
                default),
            Times.Once);
    }

    [Fact]
    public async Task JoinChannel_WithNonExistentChannel_ShouldNotifyChannelNotFound()
    {
        // Arrange
        var username = "testuser";
        var channel = "nonexistent";

        // Act
        await _hub.JoinChannel(username, channel);

        // Assert
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "ChannelNotFound",
                It.Is<object[]>(args => args.Length == 1 && (string)args[0] == channel),
                default),
            Times.Once);

        var connectedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.Channel == channel);

        Assert.Null(connectedUser);
    }

    [Fact]
    public async Task JoinChannel_WithExistingUser_ShouldUpdateConnection()
    {
        // Arrange
        var username = "existinguser";
        var channel = "general";

        // Créer le canal
        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);

        var existingUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = "old-connection-id",
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "old-server"
        };

        _db.ConnectedUsers.Add(existingUser);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(username, channel);

        // Assert
        var users = await _db.ConnectedUsers
            .Where(u => u.Username == username && u.Channel == channel)
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal("test-connection-id", users[0].ConnectionId);
    }

    [Fact]
    public async Task LeaveChannel_ShouldRemoveUserFromChannel()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = "test-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.LeaveChannel(channel);

        // Assert
        var remainingUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == "test-connection-id");

        Assert.Null(remainingUser);

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", channel, default),
            Times.Once);

        _groupMock.Verify(
            g => g.SendCoreAsync(
                "UserLeft",
                It.Is<object[]>(args => args.Length == 2),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ShouldSaveAndBroadcastMessage()
    {
        // Arrange
        var channel = "general";

        // Créer le canal
        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Username = "sender",
            Content = "Hello, World!",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Username == messageRequest.Username && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.False(message.IsDeleted);

        _groupMock.Verify(
            g => g.SendCoreAsync(
                "ReceiveMessage",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ToMutedChannel_ShouldBlockNonCreatorNonAdmin()
    {
        // Arrange
        var channel = "muted-channel";

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };

        _db.Channels.Add(mutedChannel);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Username = "sender",
            Content = "This should not be sent",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Username == messageRequest.Username && m.Content == messageRequest.Content);

        Assert.Null(message);

        _callerMock.Verify(
            c => c.SendCoreAsync(
                "MessageBlocked",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ToMutedChannel_CreatorCanSend()
    {
        // Arrange
        var channel = "muted-channel";
        var creatorUsername = "creator";

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = creatorUsername,
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };

        _db.Channels.Add(mutedChannel);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Username = creatorUsername,
            Content = "Creator can send",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Username == messageRequest.Username && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal("Creator can send", message!.Content);

        _groupMock.Verify(
            g => g.SendCoreAsync(
                "ReceiveMessage",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_ShouldSaveAndSendToRecipient()
    {
        // Arrange
        var recipientUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "recipient",
            Channel = "general",
            ConnectionId = "recipient-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        _db.ConnectedUsers.Add(recipientUser);
        await _db.SaveChangesAsync();

        // Correction: Utiliser SendPrivateMessageRequest
        var messageRequest = new SendPrivateMessageRequest
        {
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await _hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await _db.PrivateMessages
            .FirstOrDefaultAsync(m =>
                m.SenderUsername == messageRequest.SenderUsername &&
                m.RecipientUsername == messageRequest.RecipientUsername);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Content, message!.Content);
        Assert.False(message.IsRead);

        _singleClientMock.Verify(
            c => c.SendCoreAsync(
                "ReceivePrivateMessage",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Ping_ShouldUpdateLastPing()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = "test-connection-id",
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-server"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        var oldPing = user.LastPing;

        // Act
        await Task.Delay(100);
        // Correction: Ping() ne prend plus de paramètre
        await _hub.Ping();

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == "test-connection-id");

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.LastPing > oldPing);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ShouldRemoveUserAndNotifyChannel()
    {
        // Arrange
        var username = "disconnectuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = "test-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        var removedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == "test-connection-id");

        Assert.Null(removedUser);

        _groupMock.Verify(
            g => g.SendCoreAsync(
                "UserLeft",
                It.IsAny<object[]>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_ShouldMarkMessagesAndNotifySender()
    {
        // Arrange
        var senderUsername = "sender";
        var recipientUsername = "recipient";

        var recipientUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = recipientUsername,
            Channel = "general",
            ConnectionId = "test-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        var senderUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = senderUsername,
            Channel = "general",
            ConnectionId = "sender-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        _db.ConnectedUsers.AddRange(recipientUser, senderUser);

        var unreadMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = senderUsername,
            RecipientUsername = recipientUsername,
            Content = "Unread message",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeleted = false
        };

        _db.PrivateMessages.Add(unreadMessage);
        await _db.SaveChangesAsync();

        // Act
        await _hub.MarkPrivateMessagesAsRead(senderUsername);

        // Assert
        var message = await _db.PrivateMessages.FindAsync(unreadMessage.Id);
        Assert.NotNull(message);
        Assert.True(message!.IsRead);

        _singleClientMock.Verify(
            c => c.SendCoreAsync(
                "PrivateMessagesRead",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    (string)args[0] == recipientUsername),
                default),
            Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}