// tests/IrcChat.Client.Tests/Components/ChannelUsersListTests.cs
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelUsersListTests : TestContext
{
    [Fact]
    public void ChannelUsersList_WithoutChannel_ShouldNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, (string?)null)
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, []));

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithEmptyChannel_ShouldNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, []));

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithChannel_ShouldShowHeader()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, []));

        // Assert
        Assert.Contains("Utilisateurs", cut.Markup);
        Assert.Contains("(0)", cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithUsers_ShouldDisplayCount()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user2", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user3", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        Assert.Contains("(3)", cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithUsers_ShouldListAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow },
            new() { Username = "charlie", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        Assert.Contains("alice", cut.Markup);
        Assert.Contains("bob", cut.Markup);
        Assert.Contains("charlie", cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithCurrentUser_ShouldHighlight()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "testuser", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var userItems = cut.FindAll(".user-list li");
        Assert.Equal(3, userItems.Count);

        var currentUserItem = userItems.FirstOrDefault(li => li.TextContent.Contains("testuser"));
        Assert.NotNull(currentUserItem);
        Assert.Contains("current", currentUserItem.ClassList);
    }

    [Fact]
    public async Task ChannelUsersList_OnUserClick_ShouldTriggerEvent()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow }
        };

        string? clickedUsername = null;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, username => clickedUsername = username));

        // Act
        var userItems = cut.FindAll(".user-list li");
        var aliceItem = userItems.First(li => li.TextContent.Contains("alice"));
        await cut.InvokeAsync(() => aliceItem.Click());

        // Assert
        Assert.Equal("alice", clickedUsername);
    }

    [Fact]
    public async Task ChannelUsersList_OnCurrentUserClick_ShouldStillTriggerEvent()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "testuser", ConnectedAt = DateTime.UtcNow }
        };

        string? clickedUsername = null;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, username => clickedUsername = username));

        // Act
        var userItem = cut.Find(".user-list li");
        await cut.InvokeAsync(() => userItem.Click());

        // Assert
        Assert.Equal("testuser", clickedUsername);
    }

    [Fact]
    public void ChannelUsersList_WithNullUsers_ShouldShowZeroCount()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, (List<User>?)null));

        // Assert
        Assert.Contains("(0)", cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_AllUsers_ShouldHaveOnlineStatus()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user2", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var statusDots = cut.FindAll(".status-dot.online");
        Assert.Equal(2, statusDots.Count);
    }

    [Fact]
    public void ChannelUsersList_UsersOrder_ShouldBePreserved()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "zebra", ConnectedAt = DateTime.UtcNow },
            new() { Username = "alpha", ConnectedAt = DateTime.UtcNow },
            new() { Username = "beta", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var userItems = cut.FindAll(".user-list li");
        Assert.Contains("zebra", userItems[0].TextContent);
        Assert.Contains("alpha", userItems[1].TextContent);
        Assert.Contains("beta", userItems[2].TextContent);
    }

    [Fact]
    public async Task ChannelUsersList_MultipleClicks_ShouldTriggerMultipleTimes()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow }
        };

        var clickCount = 0;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, _ => clickCount++));

        // Act
        var userItem = cut.Find(".user-list li");
        await cut.InvokeAsync(() => userItem.Click());
        await cut.InvokeAsync(() => userItem.Click());
        await cut.InvokeAsync(() => userItem.Click());

        // Assert
        Assert.Equal(3, clickCount);
    }

    [Fact]
    public void ChannelUsersList_WithLongUsername_ShouldRenderCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "verylongusernamethatexceedstwentycharacters", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        Assert.Contains("verylongusernamethatexceedstwentycharacters", cut.Markup);
    }

    [Fact]
    public void ChannelUsersList_WithSpecialCharacters_ShouldRenderCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user_123", ConnectedAt = DateTime.UtcNow },
            new() { Username = "test-user", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user.name", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        Assert.Contains("user_123", cut.Markup);
        Assert.Contains("test-user", cut.Markup);
        Assert.Contains("user.name", cut.Markup);
    }
}