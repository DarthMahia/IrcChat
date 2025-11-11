// tests/IrcChat.Api.Tests/Integration/MessageEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class MessageEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SendMessage_ShouldCreateMessage()
    {
        // Arrange
        var messageRequest = new SendMessageRequest
        {
            Username = "testuser",
            Content = "Hello, World!",
            Channel = "general"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/messages", messageRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<Message>();
        Assert.NotNull(message);
        Assert.Equal("Hello, World!", message!.Content);
        Assert.Equal("testuser", message.Username);
    }

    [Fact]
    public async Task GetMessages_ShouldReturnMessagesForChannel()
    {
        // Arrange
        var channel = "general";
        await _client.PostAsJsonAsync("/api/messages", new SendMessageRequest
        {
            Username = "testuser",
            Content = "Test message 1",
            Channel = channel
        });

        await _client.PostAsJsonAsync("/api/messages", new SendMessageRequest
        {
            Username = "testuser",
            Content = "Test message 2",
            Channel = channel
        });

        // Act
        var response = await _client.GetAsync($"/api/messages/{channel}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        Assert.NotNull(messages);
        Assert.True(messages.Count >= 2);
    }
}