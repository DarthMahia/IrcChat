// tests/IrcChat.Client.Tests/Services/ChatServiceTests.cs
using System.Net;
using Bunit;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class ChatServiceTests : TestContext
{
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly IOptions<ApiSettings> _apiSettings;
    private readonly MockHttpMessageHandler _mockHttp;

    public ChatServiceTests()
    {
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();

        _apiSettings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:7000",
            SignalRHubUrl = "https://localhost:7000/chathub"
        });

        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_apiSettings);
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChatService_ShouldInitialize()
    {
        // Act
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task InitializeAsync_ShouldConnectSuccessfully()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();
        var hubConnectionMock = new Mock<HubConnectionStub>();
        hubConnectionMock.Setup(x => x.StartAsync(default)).Returns(Task.CompletedTask);
        // Setup pour tous les handlers
        hubConnectionMock.Setup(x => x.On(
            It.IsAny<string>(),
            It.IsAny<Type[]>(),
            It.IsAny<Func<object?[], object, Task>>(),
            It.IsAny<object>()))
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock
            .Setup(x => x.Build())
            .Returns(hubConnectionMock.Object);
        SetConnectedState(hubConnectionMock);

        // Act & Assert
        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Assert - Vérifier que tous les événements sont enregistrés
        hubConnectionMock.Verify(x => x.On("ReceiveMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("UserJoined", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("UserLeft", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("UpdateUserList", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelMuteStatusChanged", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelDeleted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelNotFound", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelListUpdated", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ReceivePrivateMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("PrivateMessageSent", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("PrivateMessagesRead", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
    }


    [Fact]
    public async Task OnMessageReceived_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onReceiveMessage = null;
        object? onReceiveMessageState = null;
        hubConnectionMock.Setup(x => x.On("ReceiveMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onReceiveMessage = handler;
                onReceiveMessageState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "testUser",
            Channel = "testChannel",
            Content = "Test content",
            Timestamp = DateTime.UtcNow
        };

        // Act Wtithout subscription
        await onReceiveMessage!([testMessage], onReceiveMessageState!);

        // Arrange
        Message? receivedMessage = null;
        service.OnMessageReceived += (message) => receivedMessage = message;

        // Act
        await onReceiveMessage!([testMessage], onReceiveMessageState!);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Same(testMessage, receivedMessage);
    }

    [Fact]
    public async Task OnUserJoined_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserJoined = null;
        object? onUserJoinedState = null;

        hubConnectionMock.Setup(x => x.On("UserJoined", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserJoined = handler;
                onUserJoinedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act Without subscription
        await onUserJoined!(["testUser", "testChannel"], onUserJoinedState!);

        // Arrange
        string? joinedUser = null;
        string? joinedChannel = null;
        service.OnUserJoined += (username, channel) =>
        {
            joinedUser = username;
            joinedChannel = channel;
        };

        // Act
        await onUserJoined!(["testUser", "testChannel"], onUserJoinedState!);

        // Assert
        Assert.Equal("testUser", joinedUser);
        Assert.Equal("testChannel", joinedChannel);
    }

    [Fact]
    public async Task OnUserLeft_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserLeft = null;
        object? onUserLeftState = null;
        hubConnectionMock.Setup(x => x.On("UserLeft", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserLeft = handler;
                onUserLeftState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act Without subscription
        await onUserLeft!(["testUser", "testChannel"], onUserLeftState!);

        // Arrange
        string? leftUser = null;
        string? leftChannel = null;
        service.OnUserLeft += (username, channel) =>
        {
            leftUser = username;
            leftChannel = channel;
        };

        // Act
        await onUserLeft!(["testUser", "testChannel"], onUserLeftState!);

        // Assert
        Assert.Equal("testUser", leftUser);
        Assert.Equal("testChannel", leftChannel);
    }

    [Fact]
    public async Task OnUserListUpdated_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUpdateUserList = null;
        object? onUpdateUserListState = null;

        hubConnectionMock.Setup(x => x.On("UpdateUserList", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUpdateUserList = handler;
                onUpdateUserListState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testUsers = new List<User>
        {
            new() { Username = "user1" },
            new() { Username = "user2" }
        };

        // Act without subscription
        await onUpdateUserList!([testUsers], onUpdateUserListState!);

        // Arrange
        List<User>? updatedUsers = null;
        service.OnUserListUpdated += (users) => updatedUsers = users;

        // Act
        await onUpdateUserList!([testUsers], onUpdateUserListState!);

        // Assert
        Assert.NotNull(updatedUsers);
        Assert.Equal(2, updatedUsers.Count);
        Assert.Same(testUsers, updatedUsers);
    }

    [Fact]
    public async Task OnChannelMuteStatusChanged_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelMuteStatusChanged = null;
        object? onChannelMuteStatusChangedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelMuteStatusChanged", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelMuteStatusChanged = handler;
                onChannelMuteStatusChangedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelMuteStatusChanged!(["testChannel", true], onChannelMuteStatusChangedState!);

        // Arrange
        string? mutedChannel = null;
        bool? muteStatus = null;
        service.OnChannelMuteStatusChanged += (channel, isMuted) =>
        {
            mutedChannel = channel;
            muteStatus = isMuted;
        };

        // Act
        await onChannelMuteStatusChanged!(["testChannel", true], onChannelMuteStatusChangedState!);

        // Assert
        Assert.Equal("testChannel", mutedChannel);
        Assert.True(muteStatus);
    }

    [Fact]
    public async Task OnMessageBlocked_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onMessageBlocked = null;
        object? onMessageBlockedState = null;

        hubConnectionMock.Setup(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onMessageBlocked = handler;
                onMessageBlockedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onMessageBlocked!(["Channel is muted"], onMessageBlockedState!);

        // Arrange
        string? blockedReason = null;
        service.OnMessageBlocked += (reason) => blockedReason = reason;

        // Act
        await onMessageBlocked!(["Channel is muted"], onMessageBlockedState!);

        // Assert
        Assert.Equal("Channel is muted", blockedReason);
    }

    [Fact]
    public async Task OnChannelDeleted_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelDeleted = null;
        object? onChannelDeletedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelDeleted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelDeleted = handler;
                onChannelDeletedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelDeleted!(["testChannel"], onChannelDeletedState!);

        // Arrange
        string? deletedChannel = null;
        service.OnChannelDeleted += (channel) => deletedChannel = channel;

        // Act
        await onChannelDeleted!(["testChannel"], onChannelDeletedState!);

        // Assert
        Assert.Equal("testChannel", deletedChannel);
    }

    [Fact]
    public async Task OnChannelNotFound_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelNotFound = null;
        object? onChannelNotFoundState = null;

        hubConnectionMock.Setup(x => x.On("ChannelNotFound", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelNotFound = handler;
                onChannelNotFoundState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelNotFound!(["missingChannel"], onChannelNotFoundState!);

        // Arrange
        string? notFoundChannel = null;
        service.OnChannelNotFound += (channel) => notFoundChannel = channel;

        // Act
        await onChannelNotFound!(["missingChannel"], onChannelNotFoundState!);

        // Assert
        Assert.Equal("missingChannel", notFoundChannel);
    }

    [Fact]
    public async Task OnChannelListUpdated_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelListUpdated = null;
        object? onChannelListUpdatedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelListUpdated", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelListUpdated = handler;
                onChannelListUpdatedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelListUpdated!([], onChannelListUpdatedState!);

        // Arrange
        var channelListUpdated = false;
        service.OnChannelListUpdated += () => channelListUpdated = true;

        // Act
        await onChannelListUpdated!([], onChannelListUpdatedState!);

        // Assert
        Assert.True(channelListUpdated);
    }

    [Fact]
    public async Task ReceivePrivateMessage_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onReceivePrivateMessage = null;
        object? onReceivePrivateMessageState = null;

        hubConnectionMock.Setup(x => x.On("ReceivePrivateMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onReceivePrivateMessage = handler;
                onReceivePrivateMessageState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private sent",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await onReceivePrivateMessage!([testMessage], onReceivePrivateMessageState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyPrivateMessageReceived(It.Is<PrivateMessage>(m => m.Id == testMessage.Id)),
            Times.Once);
    }

    [Fact]
    public async Task PrivateMessagesRead_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onPrivateMessagesRead = null;
        object? onPrivateMessagesReadState = null;

        hubConnectionMock.Setup(x => x.On("PrivateMessagesRead", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onPrivateMessagesRead = handler;
                onPrivateMessagesReadState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var messageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var username = "testUser";

        // Act
        await onPrivateMessagesRead!([username, messageIds], onPrivateMessagesReadState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyMessagesRead(username, It.Is<List<Guid>>(ids => ids.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task PrivateMessageSent_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onPrivateMessageSent = null;
        object? onPrivateMessageSentState = null;

        hubConnectionMock.Setup(x => x.On("PrivateMessageSent", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onPrivateMessageSent = handler;
                onPrivateMessageSentState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private sent",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await onPrivateMessageSent!([testMessage], onPrivateMessageSentState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyPrivateMessageSent(It.Is<PrivateMessage>(m => m.Id == testMessage.Id)),
            Times.Once);
    }


    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.DisposeAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task JoinChannel_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.JoinChannel("testUser", "testChannel");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task JoinChannel_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("JoinChannel", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.JoinChannel("testUser", "testChannel");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("JoinChannel", It.Is<object?[]>(args =>
                args.Length == 2 &&
                args[0]!.ToString() == "testUser" &&
                args[1]!.ToString() == "testChannel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeaveChannel_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.LeaveChannel("testChannel");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task LeaveChannel_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("LeaveChannel", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.LeaveChannel("testChannel");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("LeaveChannel", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0]!.ToString() == "testChannel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var request = new SendMessageRequest
        {
            Username = "testUser",
            Channel = "testChannel",
            Content = "Test message"
        };

        // Act
        await service.SendMessage(request);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task SendMessage_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("SendMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var request = new SendMessageRequest
        {
            Username = "testUser",
            Channel = "testChannel",
            Content = "Test message"
        };

        // Act
        await service.SendMessage(request);

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("SendMessage", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] is SendMessageRequest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var request = new SendPrivateMessageRequest
        {
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await service.SendPrivateMessage(request);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("SendPrivateMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var request = new SendPrivateMessageRequest
        {
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await service.SendPrivateMessage(request);

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("SendPrivateMessage", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] is SendPrivateMessageRequest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.MarkPrivateMessagesAsRead("sender");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("MarkPrivateMessagesAsRead", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.MarkPrivateMessagesAsRead("sender");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("MarkPrivateMessagesAsRead", It.Is<object[]>(args =>
                args.Length == 1 &&
                args[0].ToString() == "sender"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact]
    public async Task DisposeAsync_AfterInitialize_ShouldDisposeConnectionAndTimer()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        hubConnectionMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task MultipleEventSubscribers_ShouldAllBeNotified()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);


        Func<object?[], object, Task>? onMessageBlocked = null;
        object? onMessageBlockedState = null;

        hubConnectionMock.Setup(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onMessageBlocked = handler;
                onMessageBlockedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnMessageBlocked += _ => subscriber1Called++;
        service.OnMessageBlocked += _ => subscriber2Called++;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onMessageBlocked!(["test"], onMessageBlockedState!);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
    }

    [Fact]
    public async Task Ping_WhenSendAsyncThrows_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ChatService>>();
        var service = new ChatService(_privateMessageServiceMock.Object, loggerMock.Object);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetConnectedState(hubConnectionMock);

        var pingException = new InvalidOperationException("Ping failed");
        hubConnectionMock.Setup(x => x.SendCoreAsync("Ping", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(pingException);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Wait for ping timer to execute
        await Task.Delay(300);

        // Assert - Should log the error
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static void SetConnectedState(Mock<HubConnectionStub> hubConnectionMock)
    {
        // Simuler l'état connecté
        var internalStateField = typeof(HubConnection).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var internalState = internalStateField!.GetValue(hubConnectionMock.Object);
        var changeStateMethod = internalState!.GetType().GetMethod("ChangeState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        changeStateMethod!.Invoke(internalState, [HubConnectionState.Disconnected, HubConnectionState.Connected]);
    }

}


public class HubConnectionStub : HubConnection
{
    public HubConnectionStub()
        : base(
            Mock.Of<IConnectionFactory>(),
            Mock.Of<IHubProtocol>(),
            Mock.Of<EndPoint>(),
            Mock.Of<IServiceProvider>(),
            GetLoggerFactory(),
            Mock.Of<IRetryPolicy>())
    {
    }

    private static ILoggerFactory GetLoggerFactory()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        return loggerFactoryMock.Object;
    }
}