using IrcChat.Client.Models;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace IrcChat.Client.Services;

public class ChatService(IOptions<ApiSettings> apiSettings, PrivateMessageService privateMessageService) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ApiSettings _apiSettings = apiSettings.Value;
    private Timer? _pingTimer;

    // Events pour les canaux publics
    public event Action<Message>? OnMessageReceived;
    public event Action<string, string>? OnUserJoined;
    public event Action<string, string>? OnUserLeft;
    public event Action<List<User>>? OnUserListUpdated;

    // Nouveaux events pour le mute
    public event Action<string, bool>? OnChannelMuteStatusChanged;
    public event Action<string>? OnMessageBlocked;

    public async Task InitializeAsync(string? token = null)
    {
        var hubUrl = !string.IsNullOrEmpty(_apiSettings.SignalRHubUrl)
            ? _apiSettings.SignalRHubUrl
            : $"{_apiSettings.BaseUrl}/chathub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        // Handlers pour les canaux publics
        _hubConnection.On<Message>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        _hubConnection.On<string, string>("UserJoined", (username, channel) =>
        {
            OnUserJoined?.Invoke(username, channel);
        });

        _hubConnection.On<string, string>("UserLeft", (username, channel) =>
        {
            OnUserLeft?.Invoke(username, channel);
        });

        _hubConnection.On<List<User>>("UpdateUserList", users =>
        {
            OnUserListUpdated?.Invoke(users);
        });

        // Nouveaux handlers pour le mute
        _hubConnection.On<string, bool>("ChannelMuteStatusChanged", (channel, isMuted) =>
        {
            OnChannelMuteStatusChanged?.Invoke(channel, isMuted);
        });

        _hubConnection.On<string>("MessageBlocked", reason =>
        {
            OnMessageBlocked?.Invoke(reason);
        });

        // Handlers pour les messages privés
        _hubConnection.On<PrivateMessage>("ReceivePrivateMessage", message =>
        {
            privateMessageService.NotifyPrivateMessageReceived(message);
        });

        _hubConnection.On<PrivateMessage>("PrivateMessageSent", message =>
        {
            privateMessageService.NotifyPrivateMessageSent(message);
        });

        _hubConnection.On<string, List<Guid>>("PrivateMessagesRead", (username, messageIds) =>
        {
            privateMessageService.NotifyMessagesRead(username, messageIds);
        });

        await _hubConnection.StartAsync();

        _pingTimer = new Timer(async _ =>
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    await _hubConnection.SendAsync("Ping");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error sending ping: {ex.Message}");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    // Méthodes pour les canaux publics
    public async Task JoinChannel(string username, string channel)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("JoinChannel", username, channel);
    }

    public async Task LeaveChannel(string channel)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("LeaveChannel", channel);
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("SendMessage", request);
    }

    // Méthodes pour les messages privés
    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("SendPrivateMessage", request);
    }

    public async Task MarkPrivateMessagesAsRead(string senderUsername)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("MarkPrivateMessagesAsRead", senderUsername);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pingTimer != null)
        {
            await _pingTimer.DisposeAsync();
        }

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}