using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

public static class ChannelEndpoints
{
    [SuppressMessage("Performance", "CA1862", Justification = "Not translated in SQL requests")]
    public static WebApplication MapChannelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channels");

        group.MapGet("", GetChannelsAsync)
            .WithName("GetChannels")
            .WithOpenApi();

        group.MapPost("", CreateChannelAsync)
            .WithName("CreateChannel")
            .WithOpenApi();

        group.MapGet("/{channel}/users", GetConnectedUsersAsync)
            .WithName("GetConnectedUsers")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetChannelsAsync(ChatDbContext db)
    {
        var channels = await db.Channels.OrderBy(c => c.Name).ToListAsync();
        return Results.Ok(channels);
    }

    private static async Task<IResult> CreateChannelAsync(
        Channel channel,
        ChatDbContext db)
    {
        var username = channel.CreatedBy;
        if (string.IsNullOrEmpty(username))
        {
            return Results.BadRequest(new { error = "missing_username", message = "Le nom d'utilisateur est requis" });
        }

        var isReserved = await db.ReservedUsernames
            .AnyAsync(r => r.Username.ToLower() == username.ToLower());

        if (!isReserved)
        {
            return Results.Forbid();
        }

        var channelExists = await db.Channels
            .AnyAsync(c => c.Name.ToLower() == channel.Name.ToLower());

        if (channelExists)
        {
            return Results.BadRequest(new { error = "channel_exists", message = "Ce canal existe déjà" });
        }

        channel.Id = Guid.NewGuid();
        channel.CreatedAt = DateTime.UtcNow;
        channel.Name = channel.Name.Trim();

        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        return Results.Created($"/api/channels/{channel.Id}", channel);
    }

    private static async Task<IResult> GetConnectedUsersAsync(
        string channel,
        ChatDbContext db)
    {
        var users = await db.ConnectedUsers
            .Where(u => u.Channel == channel)
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                ConnectedAt = u.ConnectedAt,
                ConnectionId = u.ConnectionId
            })
            .ToListAsync();

        return Results.Ok(users);
    }
}