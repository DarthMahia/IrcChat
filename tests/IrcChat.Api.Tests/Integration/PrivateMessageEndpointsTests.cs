// tests/IrcChat.Api.Tests/Integration/PrivateMessageEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class PrivateMessageEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetConversations_ShouldReturnEmptyListForNewUser()
    {
        // Act
        var response = await _client.GetAsync("/api/private-messages/conversations/newuser");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var conversations = await response.Content
            .ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations);
        Assert.Empty(conversations);
    }

    [Fact]
    public async Task GetPrivateMessages_BetweenUsers_ShouldReturnMessages()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/private-messages/user1/with/user2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = await response.Content.ReadFromJsonAsync<List<PrivateMessage>>();
        Assert.NotNull(messages);
    }

    [Fact]
    public async Task GetUnreadCount_ShouldReturnZeroForNewUser()
    {
        // Act
        var response = await _client.GetAsync("/api/private-messages/newuser/unread-count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result!.UnreadCount);
    }

    private class UnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}