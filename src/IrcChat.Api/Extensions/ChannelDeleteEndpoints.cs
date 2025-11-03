// src/IrcChat.Api/Extensions/ChannelDeleteEndpoints.cs
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

public static class ChannelDeleteEndpoints
{
    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public static WebApplication MapChannelDeleteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channel Delete");

        group.MapDelete("/{channelName}", async (
            string channelName,
            ChatDbContext db,
            HttpContext context,
            IHubContext<ChatHub> hubContext) =>
        {
            var username = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(username))
            {
                return Results.Unauthorized();
            }

            var channel = await db.Channels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

            if (channel == null)
            {
                return Results.NotFound(new { error = "channel_not_found" });
            }

            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            var isCreator = channel.CreatedBy.ToLower() == username.ToLower();
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                return Results.Forbid();
            }

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

            // Notifier tous les clients que le canal a été supprimé
            await hubContext.Clients.Group(channelName)
                .SendAsync("ChannelDeleted", channelName, username);

            // Notifier tous les clients pour actualiser la liste des canaux
            await hubContext.Clients.All
                .SendAsync("ChannelListUpdated");

            return Results.Ok(new
            {
                channelName = channel.Name,
                deletedBy = username,
                messagesAffected = messages.Count,
                usersDisconnected = connectedUsers.Count
            });
        })
        .RequireAuthorization()
        .WithName("DeleteChannel")
        .WithOpenApi();

        return app;
    }
}