// tests/IrcChat.Client.Tests/Components/PrivateChatWindowTest.cs
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class PrivateChatWindowTests : TestContext
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;

    public PrivateChatWindowTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        Services.AddSingleton(_jsRuntimeMock.Object);
    }

    [Fact]
    public void PrivateChatWindow_OnInitialRender_ShouldLoadScrollModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true));

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")),
            Times.Once);
    }

    [Fact]
    public async Task PrivateChatWindow_ShouldAttachScrollListener()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true));

        await Task.Delay(100);

        // Assert
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task PrivateChatWindow_WhenNewMessageAdded_ShouldScrollToBottom()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                RecipientUsername = "user2",
                Content = "Hello",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, messages)
            .Add(p => p.IsConnected, true));

        await Task.Delay(100);
        mockModule.Invocations.Clear();

        // Act - Ajouter un nouveau message
        messages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "user2",
            RecipientUsername = "user1",
            Content = "Hi there",
            Timestamp = DateTime.UtcNow
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrivateChatWindow_WhenUserScrolling_ShouldNotAutoScroll()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                RecipientUsername = "user2",
                Content = "Hello",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, messages)
            .Add(p => p.IsConnected, true));

        await Task.Delay(100);

        // Simuler que l'utilisateur scrolle (n'est pas en bas)
        var instance = cut.Instance;
        instance.OnUserScroll(false);

        mockModule.Invocations.Clear();

        // Act - Ajouter un nouveau message
        messages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "user2",
            RecipientUsername = "user1",
            Content = "Hi there",
            Timestamp = DateTime.UtcNow
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert - Pas de scroll automatique
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task PrivateChatWindow_WhenSendingMessage_ShouldForceScroll()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messageSent = false;
        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnSendMessage, msg => messageSent = true));

        await Task.Delay(100);

        // Simuler que l'utilisateur scrolle (n'est pas en bas)
        var instance = cut.Instance;
        instance.OnUserScroll(false);

        mockModule.Invocations.Clear();

        // Act - Envoyer un message
        var input = cut.Find(".input-area input");
        await cut.InvokeAsync(() => input.Input("Test message"));

        var button = cut.Find(".input-area button");
        await cut.InvokeAsync(() => button.Click());

        await Task.Delay(100);

        // Assert - Doit forcer le scroll même si l'utilisateur scrollait
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.AtLeastOnce);

        Assert.True(messageSent);
    }

    [Fact]
    public async Task PrivateChatWindow_OnUserScrollToBottom_ShouldReenableAutoScroll()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                RecipientUsername = "user2",
                Content = "Hello",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, messages)
            .Add(p => p.IsConnected, true));

        await Task.Delay(100);

        var instance = cut.Instance;

        // Simuler scroll manuel
        instance.OnUserScroll(false);

        mockModule.Invocations.Clear();

        // Ajouter un message - ne devrait pas scroller
        messages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "user2",
            RecipientUsername = "user1",
            Content = "Message 1",
            Timestamp = DateTime.UtcNow
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>("scrollToBottom", It.IsAny<object[]>()),
            Times.Never);

        // Act - L'utilisateur scrolle en bas
        instance.OnUserScroll(true);

        mockModule.Invocations.Clear();

        // Ajouter un nouveau message - devrait scroller maintenant
        messages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "user2",
            RecipientUsername = "user1",
            Content = "Message 2",
            Timestamp = DateTime.UtcNow
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>("scrollToBottom", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public void PrivateChatWindow_WhenModuleLoadFails_ShouldHandleGracefully()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true));

        Assert.NotNull(cut);
    }

    [Fact]
    public async Task PrivateChatWindow_WhenDisposed_ShouldDisposeModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true));

        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task PrivateChatWindow_OnClose_ShouldTriggerCallback()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var closeCalled = false;
        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnClose, () => closeCalled = true));

        // Act
        var closeButton = cut.Find(".close-btn");
        await cut.InvokeAsync(() => closeButton.Click());

        // Assert
        Assert.True(closeCalled);
    }

    [Fact]
    public async Task PrivateChatWindow_EnterKey_ShouldSendMessage()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "attachScrollListener",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        mockModule
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var sentMessage = "";
        var cut = RenderComponent<PrivateChatWindow>(parameters => parameters
            .Add(p => p.CurrentUsername, "user1")
            .Add(p => p.OtherUsername, "user2")
            .Add(p => p.Messages, [])
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnSendMessage, msg => sentMessage = msg));

        // Act
        var input = cut.Find(".input-area input");
        await cut.InvokeAsync(() => input.Input("Test message"));
        await cut.InvokeAsync(() => input.KeyUp("Enter"));

        // Assert
        Assert.Equal("Test message", sentMessage);
    }
}