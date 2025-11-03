using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

public static class MessageEndpoints
{
    public static WebApplication MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Messages");

        group.MapGet("/{channel}", GetMessagesAsync)
            .WithName("GetMessages")
            .WithOpenApi();

        group.MapPost("", SendMessageAsync)
            .WithName("SendMessage")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetMessagesAsync(
        string channel,
        ChatDbContext db)
    {
        var messages = await db.Messages
            .Where(m => m.Channel == channel && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Results.Ok(messages);
    }

    private static async Task<IResult> SendMessageAsync(
        SendMessageRequest req,
        ChatDbContext db)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            Content = req.Content,
            Channel = req.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        return Results.Created($"/api/messages/{msg.Id}", msg);
    }
}