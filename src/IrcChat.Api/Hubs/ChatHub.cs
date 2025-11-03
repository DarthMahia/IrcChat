using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Api.Extensions;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Hubs;

public class ChatHub(
    ChatDbContext db,
    IOptions<ConnectionManagerOptions> options) : Hub
{
    private readonly string _instanceId = options.Value.GetInstanceId();

    public async Task JoinChannel(string username, string channel)
    {
        // Vérifier si le canal existe toujours
        var channelExists = await db.Channels
            .AnyAsync(c => c.Name == channel);

        if (!channelExists)
        {
            await Clients.Caller.SendAsync("ChannelNotFound", channel);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channel);

        var existingUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.Channel == channel);

        if (existingUser != null)
        {
            existingUser.ConnectionId = Context.ConnectionId;
            existingUser.LastPing = DateTime.UtcNow;
            existingUser.ServerInstanceId = _instanceId;
        }
        else
        {
            db.ConnectedUsers.Add(new ConnectedUser
            {
                Username = username,
                Channel = channel,
                ConnectionId = Context.ConnectionId,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = _instanceId
            });
        }

        await db.SaveChangesAsync();

        // Notifier les autres utilisateurs
        await Clients.Group(channel).SendAsync("UserJoined", username, channel);

        // Envoyer la liste mise à jour des utilisateurs
        var channelUsers = await GetChannelUsers(channel);
        await Clients.Group(channel).SendAsync("UpdateUserList", channelUsers);

        // Envoyer le statut mute du canal
        var channelInfo = await db.Channels
            .FirstOrDefaultAsync(c => c.Name == channel);

        if (channelInfo != null)
        {
            await Clients.Group(channel).SendAsync("ChannelMuteStatusChanged",
                channel, channelInfo.IsMuted);
        }
    }

    public async Task LeaveChannel(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);

        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == channel);

        if (connectedUser != null)
        {
            db.ConnectedUsers.Remove(connectedUser);
            await db.SaveChangesAsync();

            await Clients.Group(channel).SendAsync("UserLeft", connectedUser.Username, channel);

            var channelUsers = await GetChannelUsers(channel);
            await Clients.Group(channel).SendAsync("UpdateUserList", channelUsers);
        }
    }

    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public async Task SendMessage(SendMessageRequest request)
    {
        // Vérifier si le canal est muted
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower());

        if (channel != null && channel.IsMuted)
        {
            // Vérifier si l'utilisateur est créateur ou admin
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            var isCreator = channel.CreatedBy.ToLower() == request.Username.ToLower();
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                // Envoyer un message d'erreur uniquement à l'expéditeur
                await Clients.Caller.SendAsync("MessageBlocked",
                    "Ce salon est actuellement muet. Seul le créateur ou un administrateur peut envoyer des messages.");
                return;
            }
        }

        // Mettre à jour la dernière activité
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == request.Channel);

        if (connectedUser != null)
        {
            connectedUser.LastActivity = DateTime.UtcNow;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        await Clients.Group(request.Channel).SendAsync("ReceiveMessage", message);
    }

    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        var privateMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = request.SenderUsername,
            RecipientUsername = request.RecipientUsername,
            Content = request.Content,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeleted = false
        };

        db.PrivateMessages.Add(privateMessage);
        await db.SaveChangesAsync();

        // Trouver la connexion du destinataire
        var recipientConnection = await db.ConnectedUsers
            .Where(u => u.Username == request.RecipientUsername)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync();

        // Envoyer au destinataire s'il est connecté
        if (recipientConnection != null)
        {
            await Clients.Client(recipientConnection).SendAsync("ReceivePrivateMessage", privateMessage);
        }

        // Envoyer confirmation à l'expéditeur
        await Clients.Caller.SendAsync("PrivateMessageSent", privateMessage);
    }

    public async Task MarkPrivateMessagesAsRead(string senderUsername)
    {
        var currentUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (currentUser == null)
        {
            return;
        }

        var unreadMessages = await db.PrivateMessages
            .Where(m => m.RecipientUsername == currentUser.Username
                     && m.SenderUsername == senderUsername
                     && !m.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        await db.SaveChangesAsync();

        // Notifier l'expéditeur que ses messages ont été lus
        var senderConnection = await db.ConnectedUsers
            .Where(u => u.Username == senderUsername)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync();

        if (senderConnection != null)
        {
            await Clients.Client(senderConnection)
                .SendAsync("PrivateMessagesRead", currentUser.Username, unreadMessages.Select(m => m.Id).ToList());
        }
    }

    public async Task Ping()
    {
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user != null)
        {
            user.LastPing = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Récupérer toutes les connexions de cet utilisateur
        var userConnections = await db.ConnectedUsers
            .Where(u => u.ConnectionId == Context.ConnectionId)
            .ToListAsync();

        if (userConnections.Count != 0)
        {
            // Notifier chaque canal
            foreach (var connection in userConnections)
            {
                await Clients.Group(connection.Channel)
                    .SendAsync("UserLeft", connection.Username, connection.Channel);

                db.ConnectedUsers.Remove(connection);

                // Envoyer la liste mise à jour
                var channelUsers = await GetChannelUsers(connection.Channel);
                await Clients.Group(connection.Channel)
                    .SendAsync("UpdateUserList", channelUsers);
            }

            await db.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<User>> GetChannelUsers(string channel)
    {
        return await db.ConnectedUsers
            .Where(u => u.Channel == channel)
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                ConnectedAt = u.ConnectedAt,
                ConnectionId = u.ConnectionId
            })
            .ToListAsync();
    }
}