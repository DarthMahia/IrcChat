// tests/IrcChat.Client.Tests/Components/MessageListTests.cs
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class MessageListTests : TestContext
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;

    public MessageListTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        Services.AddSingleton(_jsRuntimeMock.Object);
    }

    [Fact]
    public void MessageList_WithEmptyMessages_ShouldRenderEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, [])
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        cut.MarkupMatches("<div class=\"messages\" diff:ignoreAttributes></div>");
    }

    [Fact]
    public void MessageList_WithMessages_ShouldRenderMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                Content = "Hi there!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        // Assert
        Assert.Equal(2, cut.FindAll(".message").Count);
        Assert.Equal("Hello!", cut.Find(".message.own .content").TextContent);
    }

    [Fact]
    public void MessageList_ShouldMarkOwnMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "currentuser",
                Content = "My message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser"));

        // Assert
        Assert.Contains("own", cut.Find(".message").ClassList);
    }

    [Fact]
    public void MessageList_OnInitialRender_ShouldLoadScrollModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")))
            .ReturnsAsync(mockModule.Object);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/scroll-helper.js")),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenNewMessageAdded_ShouldScrollToBottom()
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
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act - Ajouter un nouveau message
        messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Content = "Hi there",
            Channel = "general",
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
    public void MessageList_WhenModuleLoadFails_ShouldHandleGracefully()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        Assert.NotNull(cut);
    }

    [Fact]
    public async Task MessageList_WhenDisposed_ShouldDisposeModule()
    {
        // Arrange
        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        mockModule
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var messages = new List<Message>();

        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task MessageList_WithEmptyMessages_ShouldNotScrollInitially()
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
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, [])
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Assert - Pas de scroll pour une liste vide
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task MessageList_WhenMessageCountSame_ShouldNotScroll()
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
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Reset les appels précédents
        mockModule.Invocations.Clear();

        // Act - Re-render sans changement de count        
        cut.SetParametersAndRender(parameters => { });

        await Task.Delay(100);

        // Assert - Pas de nouveau scroll
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void MessageList_WhenScrollFails_ShouldHandleGracefully()
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
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Scroll failed"));

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert - Ne devrait pas lancer d'exception
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        Assert.NotNull(cut);
    }

    [Fact]
    public async Task MessageList_MultipleNewMessages_ShouldScrollOnce()
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
                "scrollToBottom",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Message 1",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        mockModule.Invocations.Clear();

        // Act - Ajouter plusieurs messages en une fois
        messages.AddRange(
        [
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                Content = "Message 2",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            },
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user3",
                Content = "Message 3",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        ]);

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, messages));

        await Task.Delay(100);

        // Assert - Un seul scroll pour plusieurs messages
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "scrollToBottom",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenDisposeThrows_ShouldHandleGracefully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MessageList>>();
        Services.AddSingleton(loggerMock.Object);

        var mockModule = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var disposeException = new JSException("Dispose failed");
        mockModule
            .Setup(x => x.DisposeAsync())
            .ThrowsAsync(disposeException);

        var messages = new List<Message>();

        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("libération du module de scroll")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MessageList_WhenModuleLoadThrows_ShouldLogWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MessageList>>();
        Services.AddSingleton(loggerMock.Object);

        var loadException = new JSException("Module not found");
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(loadException);

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        await Task.Delay(100);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("chargement du module de scroll")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}