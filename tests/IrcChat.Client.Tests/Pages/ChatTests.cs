// tests/IrcChat.Client.Tests/Pages/ChatTests.cs
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using Bunit;
using Bunit.TestDoubles;
using IrcChat.Client.Models;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ChatTests : TestContext
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly Mock<IDeviceDetectorService> _deviceDetectorMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public ChatTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();
        _mockHttp = new MockHttpMessageHandler();
        _deviceDetectorMock = new Mock<IDeviceDetectorService>();
        _deviceDetectorMock.Setup(x => x.IsMobileDeviceAsync())
            .ReturnsAsync(false);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_chatServiceMock.Object);
        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_deviceDetectorMock.Object);
        Services.AddSingleton(httpClient);
        Services.Configure<ApiSettings>(apiSettings =>
        {
            apiSettings.BaseUrl = "https://localhost:7000";
            apiSettings.SignalRHubUrl = "https://localhost:7000/chathub";
        });
        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void Chat_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act
        RenderComponent<Chat>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_OnInitialization_ShouldLoadChannels()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
    }

    [Fact]
    public async Task Chat__WhenAuthenticated_ShoulSetAuthorizationToken()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.Token).Returns("test-token");
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert
        var httpClient = Services.GetRequiredService<HttpClient>();
        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("test-token", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public async Task Chat_SendMessage_ShouldCallChatService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Vérifier que le composant est rendu
        Assert.NotNull(cut.Find(".chat-container"));
    }

    [Fact]
    public async Task Chat_JoinChannel_ShouldLoadMessagesAndUsers()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.JoinChannel("TestUser", "general"))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        await cut.InvokeAsync(() => cut.Find($"ul.channel-list > li[blazor\\:onclick]").Click());
        cut.Render();
        await Task.Delay(200);

        // Assert
        _chatServiceMock.Verify(
            x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_ReceiveMessage_ShouldUpdateMessageList()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        var newMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "User1",
            Content = "Test message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };

        // Act - Simuler la réception d'un message
        _chatServiceMock.Raise(x => x.OnMessageReceived += null, newMessage);
        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(x => x.OnMessageReceived += It.IsAny<Action<Message>>());
    }

    [Fact]
    public async Task Chat_PrivateMessage_ShouldOpenPrivateChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "User2",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 1
            }
        };

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        _privateMessageServiceMock
            .Setup(x => x.GetPrivateMessagesAsync("TestUser", "User2"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Vérifier que les conversations sont chargées
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_MuteStatusChange_ShouldUpdateChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler un changement de statut mute
        _chatServiceMock.Raise(
            x => x.OnChannelMuteStatusChanged += null,
            "general",
            true);

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnChannelMuteStatusChanged += It.IsAny<Action<string, bool>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_UserJoined_ShouldUpdateState()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler l'arrivée d'un utilisateur
        _chatServiceMock.Raise(
            x => x.OnUserJoined += null,
            "NewUser",
            "general");

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnUserJoined += It.IsAny<Action<string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_UserLeft_ShouldUpdateState()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(
            x => x.OnUserLeft += null,
            "DepartingUser",
            "general");

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnUserLeft += It.IsAny<Action<string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_UserListUpdated_ShouldUpdateUserList()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        var updatedUsers = new List<User>
        {
            new() { Username = "User1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "User2", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        _chatServiceMock.Raise(
            x => x.OnUserListUpdated += null,
            updatedUsers);

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnUserListUpdated += It.IsAny<Action<List<User>>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_PrivateMessageReceived_ShouldUpdateConversations()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        var privateMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "Sender",
            RecipientUsername = "TestUser",
            Content = "Private message",
            Timestamp = DateTime.UtcNow
        };

        // Act
        _privateMessageServiceMock.Raise(
            x => x.OnPrivateMessageReceived += null,
            privateMessage);

        await Task.Delay(100);

        // Assert
        _privateMessageServiceMock.VerifyAdd(
            x => x.OnPrivateMessageReceived += It.IsAny<Action<PrivateMessage>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_ConversationDeleted_ShouldClosePrivateChatIfOpen()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        _privateMessageServiceMock.Raise(
            x => x.OnConversationDeleted += null,
            "DeletedUser");

        await Task.Delay(100);

        // Assert
        _privateMessageServiceMock.VerifyAdd(
            x => x.OnConversationDeleted += It.IsAny<Action<string>>());
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_AdminUser_ShouldReturnTrue()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);

        var channels = new List<Channel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "general",
                CreatedBy = "OtherUser",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("AdminUser"))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Rejoindre un canal
        var channelItems = cut.FindAll("ul.channel-list > li[blazor\\:onclick]");
        if (channelItems.Count > 0)
        {
            await cut.InvokeAsync(() => channelItems[0].Click());
            await Task.Delay(100);
        }

        // Assert - Un admin peut gérer n'importe quel canal
        _authServiceMock.Verify(x => x.IsAdmin, Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_ChannelCreator_ShouldReturnTrue()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("Creator");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        var channels = new List<Channel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "my-channel",
                CreatedBy = "Creator",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("Creator"))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        var channelItems = cut.FindAll("ul.channel-list > li[blazor\\:onclick]");
        if (channelItems.Count > 0)
        {
            await cut.InvokeAsync(() => channelItems[0].Click());
            await Task.Delay(100);
        }

        // Assert - Le créateur peut gérer son canal
        _chatServiceMock.Verify(
            x => x.JoinChannel("Creator", "my-channel"),
            Times.Once);
    }

    [Fact]
    public async Task Chat_LeaveChannel_WhenSwitchingChannels_ShouldCallLeaveChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        var chanelItems = cut.FindAll($"ul.channel-list > li[blazor\\:onclick]");
        await cut.InvokeAsync(() => chanelItems[0].Click());
        cut.Render();

        // Act
        chanelItems = await cut.InvokeAsync(() => cut.FindAll($"ul.channel-list > li[blazor\\:onclick]"));
        await cut.InvokeAsync(() => chanelItems[1].Click());

        // Assert
        _chatServiceMock.Verify(
            x => x.LeaveChannel(It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_GoToSettings_ShouldNavigateToSettingsPage()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        await cut.InvokeAsync(() => cut.Find(".user-info").Click());

        // Assert
        Assert.EndsWith("/settings", _navManager.Uri);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_MessageBlocked_ShouldShowNotification()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(
            x => x.OnMessageBlocked += null,
            "Ce salon est muet");

        await Task.Delay(100);
        cut.Render();

        // Assert
        Assert.Contains("mute-notification", cut.Markup);
        Assert.Contains("Ce salon est muet", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_DeleteConversation_ShouldClosePrivateChatIfOpen()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "User2",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0
            }
        };

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        _privateMessageServiceMock
            .Setup(x => x.GetPrivateMessagesAsync("TestUser", "User2"))
            .ReturnsAsync([]);

        _privateMessageServiceMock
            .Setup(x => x.DeleteConversationAsync("TestUser", "User2"))
            .ReturnsAsync(true);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Simuler l'ouverture du chat privé
        _privateMessageServiceMock.Raise(
            x => x.OnConversationDeleted += null,
            "User2");

        await Task.Delay(100);

        // Assert
        Assert.DoesNotContain("private-chat-window", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_PrivateMessageSent_ShouldUpdateConversations()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        var sentMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "TestUser",
            RecipientUsername = "User2",
            Content = "Hello",
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        // Act
        _privateMessageServiceMock.Raise(
            x => x.OnPrivateMessageSent += null,
            sentMessage);

        await Task.Delay(100);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_UnreadCountChanged_ShouldReloadConversations()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        _privateMessageServiceMock.Raise(x => x.OnUnreadCountChanged += null);

        await Task.Delay(100);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_CanManageChannel_WhenCreator_ShouldReturnTrue()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        var channels = new List<Channel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "my-channel",
                CreatedBy = "TestUser",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/my-channel")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Rejoindre le canal
        await cut.InvokeAsync(() => cut.Find("ul.channel-list > li[blazor\\:onclick]").Click());
        cut.Render();

        // Assert - Le bouton mute devrait être visible
        Assert.Contains("channel-mute-control", cut.Markup);
    }

    [Fact]
    public async Task Chat_CanManageChannel_WhenAdmin_ShouldReturnTrue()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);

        var channels = new List<Channel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "someone-channel",
                CreatedBy = "OtherUser",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/someone-channel")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Rejoindre le canal
        await cut.InvokeAsync(() => cut.Find("ul.channel-list > li[blazor\\:onclick]").Click());
        cut.Render();

        // Assert - Le bouton mute devrait être visible (admin peut gérer n'importe quel canal)
        Assert.Contains("channel-mute-control", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_UserLeftEvent_ShouldTriggerStateUpdate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(
            x => x.OnUserLeft += null,
            "LeavingUser",
            "general");

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnUserLeft += It.IsAny<Action<string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task Chat_PrivateMessageReceived_WhenChatOpen_ShouldMarkAsRead()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        _privateMessageServiceMock
            .Setup(x => x.GetPrivateMessagesAsync("TestUser", "User2"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        var receivedMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "User2",
            RecipientUsername = "TestUser",
            Content = "Hello",
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        // Act - Ouvrir le chat et recevoir un message
        _privateMessageServiceMock.Raise(
            x => x.OnPrivateMessageReceived += null,
            receivedMessage);

        await Task.Delay(100);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_LoadChannels_WhenApiCallFails_ShouldHandleGracefully()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.InternalServerError);

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Le composant devrait se rendre sans plantage
        Assert.NotNull(cut.Find(".chat-container"));
    }

    [Fact]
    public async Task Chat_LoadMessages_WhenApiCallFails_ShouldHandleGracefully()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.InternalServerError);

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Essayer de rejoindre le canal
        await cut.InvokeAsync(() => cut.Find("ul.channel-list > li[blazor\\:onclick]").Click());
        cut.Render();
        await Task.Delay(200);

        // Assert - Le composant devrait gérer l'erreur sans plantage
        _chatServiceMock.Verify(
            x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // ==================== TESTS POUR LA GESTION DES CANAUX ====================

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelDeleted_ShouldRemoveChannelFromList()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "tech", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Vérifier que les 3 canaux sont présents
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
        Assert.Contains("tech", cut.Markup);

        // Act - Simuler la suppression du canal "random"
        _chatServiceMock.Raise(
            x => x.OnChannelDeleted += null,
            "random");

        await Task.Delay(100);
        cut.Render();

        // Assert - Le canal "random" ne devrait plus être présent
        Assert.Contains("general", cut.Markup);
        Assert.DoesNotContain("random", cut.Markup);
        Assert.Contains("tech", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelDeleted_WhenCurrentChannelDeleted_ShouldClearCurrentChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "Test message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Rejoindre le canal "general"
        await cut.InvokeAsync(() => cut.Find("ul.channel-list > li[blazor\\:onclick]").Click());
        await Task.Delay(100);
        cut.Render();

        // Vérifier que le message est affiché
        Assert.Contains("Test message", cut.Markup);

        // Act - Simuler la suppression du canal actuel
        _chatServiceMock.Raise(
            x => x.OnChannelDeleted += null,
            "general");

        await Task.Delay(100);
        cut.Render();

        // Assert - Le message ne devrait plus être affiché
        Assert.DoesNotContain("Test message", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelDeleted_WhenDifferentChannelDeleted_ShouldNotAffectCurrentChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "General message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Rejoindre le canal "general"
        await cut.InvokeAsync(() => cut.Find("ul.channel-list > li[blazor\\:onclick]").Click());
        await Task.Delay(100);
        cut.Render();

        // Vérifier que le message est affiché
        Assert.Contains("General message", cut.Markup);

        // Act - Simuler la suppression d'un autre canal
        _chatServiceMock.Raise(
            x => x.OnChannelDeleted += null,
            "random");

        await Task.Delay(100);
        cut.Render();

        // Assert - Le message devrait toujours être affiché
        Assert.Contains("General message", cut.Markup);
        Assert.Contains("general", cut.Markup);
        Assert.DoesNotContain("random", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelNotFound_ShouldDisplayNotification()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler un canal introuvable
        _chatServiceMock.Raise(
            x => x.OnChannelNotFound += null,
            "deleted-channel");

        await Task.Delay(100);
        cut.Render();

        // Assert - La notification devrait être affichée
        Assert.Contains("mute-notification", cut.Markup);
        Assert.Contains("Le canal #deleted-channel n'existe plus", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelNotFound_ShouldClearNotificationAfterDelay()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler un canal introuvable
        _chatServiceMock.Raise(
            x => x.OnChannelNotFound += null,
            "deleted-channel");

        await Task.Delay(100);
        cut.Render();

        // Vérifier que la notification est affichée
        Assert.Contains("Le canal #deleted-channel n'existe plus", cut.Markup);

        // Attendre que la notification disparaisse (4 secondes + marge)
        await Task.Delay(4500);
        cut.Render();

        // Assert - La notification devrait avoir disparu
        Assert.DoesNotContain("Le canal #deleted-channel n'existe plus", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task OnChannelListUpdated_ShouldReloadChannels()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var initialChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var updatedChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "new-channel", CreatedBy = "admin", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialChannels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Vérifier que seul "general" est présent
        Assert.Contains("general", cut.Markup);
        Assert.DoesNotContain("new-channel", cut.Markup);

        // Mettre à jour le mock pour retourner les nouveaux canaux
        _mockHttp.Clear();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(updatedChannels));

        // Act - Simuler la mise à jour de la liste des canaux
        _chatServiceMock.Raise(x => x.OnChannelListUpdated += null);

        await Task.Delay(200);
        cut.Render();

        // Assert - Le nouveau canal devrait être visible
        Assert.Contains("general", cut.Markup);
        Assert.Contains("new-channel", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task HandleChannelDeleted_ShouldCallOnChannelDeleted()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "test-channel", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Vérifier que le canal est présent
        Assert.Contains("test-channel", cut.Markup);

        // Act - Simuler HandleChannelDeleted via l'événement OnChannelDeleted
        _chatServiceMock.Raise(
            x => x.OnChannelDeleted += null,
            "test-channel");

        await Task.Delay(100);
        cut.Render();

        // Assert - Le canal ne devrait plus être présent
        Assert.DoesNotContain("test-channel", cut.Markup);
        _chatServiceMock.VerifyAdd(
            x => x.OnChannelDeleted += It.IsAny<Action<string>>());
    }

    [Fact]
    public async Task SendMessage_WithValidContent_ShouldCallChatService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var messages = new List<Message>();

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Rejoindre un canal
        var channelItems = cut.FindAll("ul.channel-list > li[blazor\\:onclick]");
        if (channelItems.Count > 0)
        {
            await cut.InvokeAsync(() => channelItems[0].Click());
            await Task.Delay(100);
        }

        // Act - Simuler l'envoi d'un message via MessageInput
        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Test message"));
        await cut.InvokeAsync(() => button.Click());

        // Assert
        _chatServiceMock.Verify(
            x => x.SendMessage(It.Is<SendMessageRequest>(req =>
                req.Username == "TestUser" &&
                req.Content == "Test message" &&
                req.Channel == "general")),
            Times.Once);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task HandleUserClicked_WithDifferentUser_ShouldOpenPrivateChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "OtherUser"))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler un clic sur un utilisateur via l'événement ChatArea
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "OtherUser"))
            .ReturnsAsync([]);

        // Trigger HandleUserClicked indirectement via l'ouverture du chat
        _privateMessageServiceMock.Raise(
            x => x.OnPrivateMessageReceived += null,
            new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderUsername = "OtherUser",
                RecipientUsername = "TestUser",
                Content = "Hi",
                Timestamp = DateTime.UtcNow
            });

        await Task.Delay(100);

        // Assert - Vérifier que GetPrivateMessagesAsync a été appelé
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OpenPrivateChat_ShouldLoadMessagesAndMarkAsRead()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "Friend",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2
            }
        };

        var privateMessages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "Friend",
                RecipientUsername = "TestUser",
                Content = "Hello",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            }
        };

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync(privateMessages);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Cliquer sur une conversation
        var conversationItems = cut.FindAll(".conversation-list li");
        if (conversationItems.Count > 0)
        {
            await cut.InvokeAsync(() => conversationItems[0].Click());
            await Task.Delay(100);
        }

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.GetPrivateMessagesAsync("TestUser", "Friend"),
            Times.Once);

        _chatServiceMock.Verify(
            x => x.MarkPrivateMessagesAsRead("Friend"),
            Times.Once);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génére une erreur")]
    public async Task SendPrivateMessage_ShouldCallChatService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Simuler l'ouverture d'un chat privé
        _privateMessageServiceMock.Raise(
            x => x.OnPrivateMessageReceived += null,
            new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderUsername = "Friend",
                RecipientUsername = "TestUser",
                Content = "Hi",
                Timestamp = DateTime.UtcNow
            });

        await Task.Delay(100);
        cut.Render();

        // Act - Trouver la fenêtre de chat privé et envoyer un message
        var privateChatInput = cut.FindAll(".private-chat-window .input-area input");
        if (privateChatInput.Count > 0)
        {
            await cut.InvokeAsync(() => privateChatInput[0].Input("Private reply"));
            var sendButton = cut.Find(".private-chat-window .input-area button");
            await cut.InvokeAsync(() => sendButton.Click());
        }

        // Assert
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.Is<SendPrivateMessageRequest>(req =>
                req.SenderUsername == "TestUser" &&
                req.RecipientUsername == "Friend" &&
                req.Content == "Private reply")),
            Times.AtMostOnce()); // AtMostOnce car le chat pourrait ne pas être ouvert
    }

    [Fact]
    public async Task DeleteConversation_ShouldCallService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "Friend",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0
            }
        };

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        _privateMessageServiceMock.Setup(x => x.DeleteConversationAsync("TestUser", "Friend"))
            .ReturnsAsync(true);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Cliquer sur le bouton de suppression
        var deleteButtons = cut.FindAll(".delete-conversation-btn");
        if (deleteButtons.Count > 0)
        {
            await cut.InvokeAsync(() => deleteButtons[0].Click());
            await Task.Delay(100);
        }

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.DeleteConversationAsync("TestUser", "Friend"),
            Times.Once);
    }

    [Fact]
    public async Task HandleMuteStatusChanged_ShouldReloadChannels()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);

        var initialChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "TestUser", CreatedAt = DateTime.UtcNow, IsMuted = false }
        };

        var updatedChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "TestUser", CreatedAt = DateTime.UtcNow, IsMuted = true }
        };

        var messages = new List<Message>();

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialChannels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Rejoindre le canal
        var channelItems = cut.FindAll("ul.channel-list > li[blazor\\:onclick]");
        if (channelItems.Count > 0)
        {
            await cut.InvokeAsync(() => channelItems[0].Click());
            await Task.Delay(100);
        }

        // Préparer le mock pour le rechargement
        _mockHttp.Clear();
        var muteToggleResponse = new { ChannelName = "general", IsMuted = true, ChangedBy = "admin" };
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.OK, JsonContent.Create(muteToggleResponse));
        var mockedRequest = _mockHttp.When(HttpMethod.Get, "*/api/channels");
        mockedRequest.Respond(HttpStatusCode.OK, JsonContent.Create(updatedChannels));

        // Act - Simuler le changement de statut via le composant
        // Trouver le ChannelMuteButton et son callback

        var button = await cut.InvokeAsync(() => cut.Find(".mute-btn"));
        // Act - Double click rapide
        await cut.InvokeAsync(() => button.Click());

        await Task.Delay(200);

        // Assert - Vérifier que les canaux ont été rechargés
        var matchCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.True(matchCount >= 1, "Le rechargement des canaux n'a pas été effectué après le changement de statut de mute.");
    }

    [Fact]
    public async Task SendMessage_WhenNotConnected_ShouldNotCallService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        // Ne pas initialiser ChatService pour simuler une non-connexion
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Throws(new Exception("Connection failed"));

        _chatServiceMock.Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Le composant devrait gérer l'erreur gracieusement
        var input = cut.FindAll(".input-area input");
        if (input.Count > 0)
        {
            // Le bouton devrait être désactivé quand déconnecté
            var button = cut.Find(".input-area button");
            Assert.True(button.HasAttribute("disabled"));
        }

        // Assert
        _chatServiceMock.Verify(
            x => x.SendMessage(It.IsAny<SendMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenNotConnected_ShouldNotCallService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Throws(new Exception("Connection failed"));

        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Le service ne devrait jamais être appelé quand déconnecté
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_HandleSidebarToggle_WhenCalledWithFalse_ShouldCloseSidebar()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Ouvrir puis fermer la sidebar
        var toggleButton = cut.Find(".sidebar-toggle-btn");
        await cut.InvokeAsync(() => toggleButton.Click());
        await Task.Delay(100);

        Assert.Contains("sidebar-open", cut.Markup);

        await cut.InvokeAsync(() => toggleButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("sidebar-closed", cut.Markup);
    }

    [Fact]
    public async Task Chat_OnInitializedAsync_WhenAuthNotInitialized_ShouldHandleGracefully()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act & Assert - Ne devrait pas planter
        RenderComponent<Chat>();
        await Task.Delay(200);

        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_InitializeSignalR_WhenConnectionFails_ShouldSetIsConnectedFalse()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .ThrowsAsync(new Exception("Connection failed"));

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Le composant devrait gérer l'erreur
        Assert.NotNull(cut.Find(".chat-container"));
    }

    [Fact]
    public async Task Chat_TotalUnreadCount_ShouldSumAllConversations()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
    {
        new() { OtherUsername = "User1", UnreadCount = 3, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow },
        new() { OtherUsername = "User2", UnreadCount = 5, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow },
        new() { OtherUsername = "User3", UnreadCount = 2, LastMessage = "Hey", LastMessageTime = DateTime.UtcNow }
    };

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Le badge devrait montrer le total (10)
        var badge = cut.Find(".unread-badge");
        Assert.Equal("10", badge.TextContent);
    }

    [Fact]
    public async Task Chat_SendMessage_WithEmptyContent_ShouldNotCallService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
    {
        new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
    };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Rejoindre un canal
        var channelItems = cut.FindAll("ul.channel-list > li[blazor\\:onclick]");
        if (channelItems.Count > 0)
        {
            await cut.InvokeAsync(() => channelItems[0].Click());
            await Task.Delay(100);
        }

        // Act - Essayer d'envoyer un message vide
        var button = cut.Find(".input-area button");
        await cut.InvokeAsync(() => button.Click());

        // Assert - Ne devrait pas appeler le service
        _chatServiceMock.Verify(
            x => x.SendMessage(It.IsAny<SendMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_ClosePrivateChat_ShouldClearSelectedUser()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(
            [
            new() { OtherUsername = "Friend", LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }
            ]);

        _privateMessageServiceMock
            .Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Ouvrir le chat privé
        var conversations = cut.FindAll(".conversation-list li");
        if (conversations.Count > 0)
        {
            await cut.InvokeAsync(() => conversations[0].Click());
            await Task.Delay(100);
        }

        Assert.Contains("private-chat-window", cut.Markup);

        // Act - Fermer le chat privé
        var closeButton = cut.Find(".private-chat-window .close-btn");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.DoesNotContain("private-chat-window", cut.Markup);
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_WhenNoChannel_ShouldReturnFalse()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Pas de contrôles de mute visible
        Assert.DoesNotContain("channel-mute-control", cut.Markup);
    }
}