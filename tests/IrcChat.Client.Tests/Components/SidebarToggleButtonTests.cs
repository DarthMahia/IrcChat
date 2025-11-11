// Tests supplémentaires pour SidebarToggleButton.razor
// Ajouter à tests/IrcChat.Client.Tests/Components/SidebarToggleButtonTests.cs

using Bunit;
using IrcChat.Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class SidebarToggleButtonTests : TestContext
{
    [Fact]
    public void SidebarToggleButton_WhenIsOpenTrue_ShouldHaveOpenClass()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.UnreadCount, 0));

        // Assert
        var button = cut.Find("button.sidebar-toggle-btn");
        Assert.Contains("open", button.ClassName);
    }

    [Fact]
    public void SidebarToggleButton_WhenIsOpenFalse_ShouldNotHaveOpenClass()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 0));

        // Assert
        var button = cut.Find("button.sidebar-toggle-btn");
        Assert.DoesNotContain("open", button.ClassName);
    }

    [Fact]
    public void SidebarToggleButton_WhenUnreadCountZero_ShouldNotShowBadge()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 0));

        // Assert
        var badges = cut.FindAll(".unread-badge");
        Assert.Empty(badges);
    }

    [Fact]
    public void SidebarToggleButton_WhenUnreadCountPositive_ShouldShowBadge()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 5));

        // Assert
        var badge = cut.Find(".unread-badge");
        Assert.Equal("5", badge.TextContent);
    }

    [Fact]
    public void SidebarToggleButton_WhenUnreadCountOver99_ShouldShow99Plus()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 150));

        // Assert
        var badge = cut.Find(".unread-badge");
        Assert.Equal("99+", badge.TextContent);
    }

    [Fact]
    public void SidebarToggleButton_WhenUnreadCountExactly99_ShouldShow99()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 99));

        // Assert
        var badge = cut.Find(".unread-badge");
        Assert.Equal("99", badge.TextContent);
    }

    [Fact]
    public void SidebarToggleButton_WhenUnreadCount100_ShouldShow99Plus()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 100));

        // Assert
        var badge = cut.Find(".unread-badge");
        Assert.Equal("99+", badge.TextContent);
    }

    [Fact]
    public async Task SidebarToggleButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var newState = false;

        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 0)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<bool>(this, state =>
            {
                callbackInvoked = true;
                newState = state;
            })));

        // Act
        var button = cut.Find("button.sidebar-toggle-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.True(callbackInvoked);
        Assert.True(newState);
    }

    [Fact]
    public async Task SidebarToggleButton_WhenClickedWhileOpen_ShouldToggleToFalse()
    {
        // Arrange
        var callbackInvoked = false;
        var newState = true;

        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.UnreadCount, 0)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<bool>(this, state =>
            {
                callbackInvoked = true;
                newState = state;
            })));

        // Act
        var button = cut.Find("button.sidebar-toggle-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.True(callbackInvoked);
        Assert.False(newState);
    }

    [Fact]
    public void SidebarToggleButton_ShouldHaveMenuTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 0));

        // Assert
        var button = cut.Find("button.sidebar-toggle-btn");
        Assert.Equal("Menu", button.GetAttribute("title"));
    }

    [Fact]
    public void SidebarToggleButton_ShouldHaveHamburgerIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, 0));

        // Assert
        var hamburger = cut.Find(".hamburger");
        var spans = hamburger.QuerySelectorAll("span");
        Assert.Equal(3, spans.Length);
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(10, "10")]
    [InlineData(50, "50")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(200, "99+")]
    [InlineData(999, "99+")]
    public void SidebarToggleButton_VariousUnreadCounts_ShouldDisplayCorrectly(int count, string expected)
    {
        // Arrange & Act
        var cut = RenderComponent<SidebarToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UnreadCount, count));

        // Assert
        var badge = cut.Find(".unread-badge");
        Assert.Equal(expected, badge.TextContent);
    }
}