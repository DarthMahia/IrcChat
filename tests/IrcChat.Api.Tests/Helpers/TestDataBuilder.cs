// tests/IrcChat.Api.Tests/Helpers/TestDataBuilder.cs
using IrcChat.Shared.Models;

namespace IrcChat.Api.Tests.Helpers;

public static class TestDataBuilder
{
    public static Message CreateMessage(
        string username = "testuser",
        string content = "Test message",
        string channel = "general")
    {
        return new Message
        {
            Id = Guid.NewGuid(),
            Username = username,
            Content = content,
            Channel = channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public static Channel CreateChannel(
        string name = "test-channel",
        string createdBy = "testuser",
        string? activeManager = null)
    {
        return new Channel
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedBy = createdBy,
            ActiveManager = activeManager ?? createdBy, // Par défaut, le créateur est le manager
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
    }

    public static ConnectedUser CreateConnectedUser(
        string username = "testuser",
        string channel = "general",
        string connectionId = "test-connection")
    {
        return new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = connectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };
    }

    public static ReservedUsername CreateReservedUsername(
        string username = "reserved_user",
        ExternalAuthProvider provider = ExternalAuthProvider.Google,
        bool isAdmin = false)
    {
        return new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = username,
            Provider = provider,
            ExternalUserId = Guid.NewGuid().ToString(),
            Email = $"{username}@example.com",
            DisplayName = username,
            AvatarUrl = $"https://example.com/avatar/{username}.jpg",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = isAdmin
        };
    }

    public static PrivateMessage CreatePrivateMessage(
        string sender = "user1",
        string recipient = "user2",
        string content = "Private test message")
    {
        return new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = sender,
            RecipientUsername = recipient,
            Content = content,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeleted = false
        };
    }
}