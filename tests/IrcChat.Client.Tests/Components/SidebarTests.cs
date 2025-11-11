// tests/IrcChat.Client.Tests/Components/SidebarTests.cs
using System.Threading.Tasks;
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class SidebarTests : TestContext
{
    [Fact]
    public void Sidebar_WhenOpen_ShouldHaveOpenClass()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("open", cut.Find(".sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_WhenClosed_ShouldHaveClosedClass()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("closed", cut.Find(".sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_ShouldDisplayUsername()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "JohnDoe")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("JohnDoe", cut.Markup);
    }

    [Fact]
    public void Sidebar_WithAvatar_ShouldDisplayAvatar()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.AvatarUrl, "https://example.com/avatar.jpg")
            .Add(p => p.Channels, []));

        // Assert
        var avatar = cut.Find(".avatar");
        Assert.Equal("https://example.com/avatar.jpg", avatar.GetAttribute("src"));
    }

    [Fact]
    public void Sidebar_WithoutAvatar_ShouldDisplayPlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.AvatarUrl, null)
            .Add(p => p.Channels, []));

        // Assert
        Assert.NotNull(cut.Find(".avatar-placeholder"));
    }

    [Fact]
    public void Sidebar_WhenOAuthUser_ShouldDisplayVerifiedBadge()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.IsOAuthUser, true)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("✓ Réservé", cut.Markup);
    }

    [Fact]
    public void Sidebar_WhenAdmin_ShouldDisplayAdminBadge()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "AdminUser")
            .Add(p => p.IsOAuthUser, true)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("⚡ Admin", cut.Markup);
    }

    [Fact]
    public void Sidebar_WhenGuestUser_ShouldDisplayGuestBadge()
    {
        // Arrange & Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "GuestUser")
            .Add(p => p.IsOAuthUser, false)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("Invité", cut.Markup);
    }

    [Fact]
    public void Sidebar_ShouldDisplayChannelsList()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "tech", CreatedBy = "user1", CreatedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels));

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
        Assert.Contains("tech", cut.Markup);
    }

    [Fact]
    public void Sidebar_CurrentChannel_ShouldBeHighlighted()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels)
            .Add(p => p.CurrentChannel, "general"));

        // Assert
        var activeChannel = cut.FindAll(".channel-list li.active");
        Assert.Single(activeChannel);
        Assert.Contains("general", activeChannel[0].TextContent);
    }

    [Fact]
    public void Sidebar_ShouldDisplayPrivateConversations()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "Friend1",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2
            },
            new()
            {
                OtherUsername = "Friend2",
                LastMessage = "Hi",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0
            }
        };

        // Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.PrivateConversations, conversations));

        // Assert
        Assert.Contains("Friend1", cut.Markup);
        Assert.Contains("Friend2", cut.Markup);
    }

    [Fact]
    public async Task Sidebar_UserInfoClick_ShouldInvokeCallback()
    {
        // Arrange
        var userInfoClickedInvoked = false;

        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.OnUserInfoClicked, () => userInfoClickedInvoked = true));

        // Act
        var userInfo = cut.Find(".user-info");
        await cut.InvokeAsync(() => userInfo.Click());

        // Assert
        Assert.True(userInfoClickedInvoked);
    }

    [Fact]
    public void Sidebar_WithUnreadMessages_ShouldDisplayBadge()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "Friend",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 5
            }
        };

        // Act
        var cut = RenderComponent<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.PrivateConversations, conversations));

        // Assert
        Assert.Contains("5", cut.Markup);
        Assert.NotNull(cut.Find(".unread-count"));
    }
}