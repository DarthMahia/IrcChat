using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class PrivateMessageEndpoints
{
    public static WebApplication MapPrivateMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/private-messages")
            .WithTags("Private Messages");

        // Récupérer les conversations
        group.MapGet("/conversations/{username}", GetConversationsAsync)
            .WithName("GetConversations")
            .WithOpenApi();

        // Récupérer les messages d'une conversation
        group.MapGet("/{username}/with/{otherUsername}", GetPrivateMessagesAsync)
            .WithName("GetPrivateMessages")
            .WithOpenApi();

        // Récupérer le nombre de messages non lus
        group.MapGet("/{username}/unread-count", GetUnreadCountAsync)
            .WithName("GetUnreadCount")
            .WithOpenApi();

        // Supprimer une conversation (soft delete)
        group.MapDelete("/{username}/conversation/{otherUsername}", DeleteConversationAsync)
            .WithName("DeleteConversation")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetConversationsAsync(
        string username,
        ChatDbContext db)
    {
        var conversations = await db.PrivateMessages
            .Where(m => (m.SenderUsername == username || m.RecipientUsername == username)
                     && !m.IsDeleted)
            .GroupBy(m => m.SenderUsername == username ? m.RecipientUsername : m.SenderUsername)
            .Select(g => new
            {
                OtherUsername = g.Key,
                LastMessage = g.OrderByDescending(m => m.Timestamp).First(),
                UnreadCount = g.Count(m => m.RecipientUsername == username && !m.IsRead)
            })
            .ToListAsync();

        var result = conversations.Select(c => new PrivateConversation
        {
            OtherUsername = c.OtherUsername,
            LastMessage = c.LastMessage.Content,
            LastMessageTime = c.LastMessage.Timestamp,
            UnreadCount = c.UnreadCount
        }).OrderByDescending(c => c.LastMessageTime);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPrivateMessagesAsync(
        string username,
        string otherUsername,
        ChatDbContext db)
    {
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUsername == username && m.RecipientUsername == otherUsername) ||
                        (m.SenderUsername == otherUsername && m.RecipientUsername == username))
                     && !m.IsDeleted)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Results.Ok(messages);
    }

    private static async Task<IResult> GetUnreadCountAsync(
        string username,
        ChatDbContext db)
    {
        var count = await db.PrivateMessages
            .CountAsync(m => m.RecipientUsername == username && !m.IsRead && !m.IsDeleted);

        return Results.Ok(new { UnreadCount = count });
    }

    private static async Task<IResult> DeleteConversationAsync(
        string username,
        string otherUsername,
        ChatDbContext db)
    {
        // Récupérer tous les messages de la conversation pour cet utilisateur
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUsername == username && m.RecipientUsername == otherUsername) ||
                        (m.SenderUsername == otherUsername && m.RecipientUsername == username))
                     && !m.IsDeleted)
            .ToListAsync();

        if (messages.Count == 0)
        {
            return Results.NotFound();
        }

        // Marquer tous les messages comme supprimés
        foreach (var message in messages)
        {
            message.IsDeleted = true;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new { Deleted = messages.Count });
    }
}