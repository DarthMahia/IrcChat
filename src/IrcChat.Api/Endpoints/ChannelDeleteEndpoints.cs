using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Api.Extensions;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class ChannelDeleteEndpoints
{
    public static WebApplication MapChannelDeleteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channel Delete");

        group.MapDelete("/{channelName}", DeleteChannelAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("DeleteChannel")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> DeleteChannelAsync(
        string channelName,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext)
    {
        // Récupérer le canal (on sait qu'il existe car la policy l'a vérifié)
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

        if (channel == null)
        {
            return Results.NotFound(new { error = "channel_not_found" });
        }

        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        // Supprimer tous les utilisateurs connectés au canal
        var connectedUsers = await db.ConnectedUsers
            .Where(u => u.Channel.ToLower() == channelName.ToLower())
            .ToListAsync();

        db.ConnectedUsers.RemoveRange(connectedUsers);

        // Marquer tous les messages comme supprimés (soft delete)
        var messages = await db.Messages
            .Where(m => m.Channel.ToLower() == channelName.ToLower())
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsDeleted = true;
        }

        // Supprimer le canal
        db.Channels.Remove(channel);
        await db.SaveChangesAsync();

        // Notifier tous les clients
        await hubContext.Clients.Group(channelName)
            .SendAsync("ChannelDeleted", channelName, username);

        await hubContext.Clients.All
            .SendAsync("ChannelListUpdated");

        return Results.Ok(new
        {
            channelName = channel.Name,
            deletedBy = username,
            messagesAffected = messages.Count,
            usersDisconnected = connectedUsers.Count
        });
    }
}