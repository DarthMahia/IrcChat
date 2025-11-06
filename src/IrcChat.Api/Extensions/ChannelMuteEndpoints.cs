using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class ChannelMuteEndpoints
{
    public static WebApplication MapChannelMuteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channel Mute");

        // Toggle mute status
        group.MapPost("/{channelName}/toggle-mute", async (
            string channelName,
            ChatDbContext db,
            HttpContext context,
            IHubContext<ChatHub> hubContext) =>
        {
            // Récupérer l'utilisateur actuel
            var username = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(username))
            {
                return Results.Unauthorized();
            }

            // Trouver le canal
            var channel = await db.Channels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

            if (channel == null)
            {
                return Results.NotFound(new { error = "channel_not_found" });
            }

            // Vérifier si l'utilisateur est le créateur ou admin
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            var isCreator = channel.CreatedBy.ToLower() == username.ToLower();
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                return Results.Forbid();
            }

            // Toggle le statut mute
            channel.IsMuted = !channel.IsMuted;

            // Si un admin démute le salon, il devient le manager actif
            // Si le créateur démute son salon, il redevient le manager actif
            if (!channel.IsMuted)
            {
                channel.ActiveManager = username;
            }

            await db.SaveChangesAsync();

            // Notifier le changement de statut mute
            await hubContext.Clients.Group(channelName)
                .SendAsync("ChannelMuteStatusChanged", channelName, channel.IsMuted);

            return Results.Ok(new
            {
                channelName = channel.Name,
                isMuted = channel.IsMuted,
                changedBy = username
            });
        })
        .RequireAuthorization()
        .WithName("ToggleChannelMute")
        .WithOpenApi();

        return app;
    }
}