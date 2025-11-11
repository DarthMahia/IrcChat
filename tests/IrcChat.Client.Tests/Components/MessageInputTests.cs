// tests/IrcChat.Client.Tests/Components/MessageInputTests.cs
using Bunit;
using IrcChat.Client.Components;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class MessageInputTests : TestContext
{
    [Fact]
    public void MessageInput_WhenDisconnected_ShouldDisableInput()
    {
        // Act
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false));

        // Assert
        var input = cut.Find("input");
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public void MessageInput_WhenConnected_ShouldEnableInput()
    {
        // Act
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true));

        // Assert
        var input = cut.Find("input");
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public async Task MessageInput_OnSendClick_ShouldTriggerEvent()
    {
        // Arrange
        string? sentMessage = null;
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnSendMessage, EventCallback.Factory.Create<string>(
                this, (msg) => sentMessage = msg)));

        var input = cut.Find("input");
        var button = cut.Find("button");

        // Act
        await cut.InvokeAsync(() => input.Input("Test message"));
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Equal("Test message", sentMessage);
    }
}