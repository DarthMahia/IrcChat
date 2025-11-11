using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class ChannelMuteEndpoints
{
    public static WebApplication MapChannelMuteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channel Mute");

        group.MapPost("/{channelName}/toggle-mute", ToggleMuteAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("ToggleChannelMute")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> ToggleMuteAsync(
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

        // Toggle le statut mute
        channel.IsMuted = !channel.IsMuted;

        if (!channel.IsMuted)
        {
            channel.ActiveManager = username;
        }

        await db.SaveChangesAsync();

        await hubContext.Clients.Group(channelName)
            .SendAsync("ChannelMuteStatusChanged", channelName, channel.IsMuted);

        return Results.Ok(new
        {
            channelName = channel.Name,
            isMuted = channel.IsMuted,
            changedBy = username
        });
    }
}