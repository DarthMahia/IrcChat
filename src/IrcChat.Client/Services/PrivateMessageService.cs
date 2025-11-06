// src/IrcChat.Client/Services/PrivateMessageService.cs
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public class PrivateMessageService(HttpClient httpClient) : IPrivateMessageService
{
    public event Action<PrivateMessage>? OnPrivateMessageReceived;
    public event Action<PrivateMessage>? OnPrivateMessageSent;
    public event Action<string, List<Guid>>? OnMessagesRead;
    public event Action? OnUnreadCountChanged;
    public event Action<string>? OnConversationDeleted;

    public void NotifyPrivateMessageReceived(PrivateMessage message)
    {
        OnPrivateMessageReceived?.Invoke(message);
        OnUnreadCountChanged?.Invoke();
    }

    public void NotifyPrivateMessageSent(PrivateMessage message) => OnPrivateMessageSent?.Invoke(message);

    public void NotifyMessagesRead(string username, List<Guid> messageIds) => OnMessagesRead?.Invoke(username, messageIds);

    public async Task<List<PrivateConversation>> GetConversationsAsync(string username)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateConversation>>(
                $"/api/private-messages/conversations/{username}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<PrivateMessage>> GetPrivateMessagesAsync(string username, string otherUsername)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateMessage>>(
                $"/api/private-messages/{username}/with/{otherUsername}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync(string username)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<UnreadCountResponse>(
                $"/api/private-messages/{username}/unread-count");
            return result?.UnreadCount ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> DeleteConversationAsync(string username, string otherUsername)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/api/private-messages/{username}/conversation/{otherUsername}");

            if (response.IsSuccessStatusCode)
            {
                OnConversationDeleted?.Invoke(otherUsername);
                OnUnreadCountChanged?.Invoke();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // Suppression des warnings SonarQube pour les propriétés utilisées par la désérialisation JSON
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Utilisé par la désérialisation JSON")]
    [SuppressMessage("Minor Code Smell", "S1144:Unused private types or members should be removed", Justification = "Utilisé par la désérialisation JSON")]
    [SuppressMessage("Minor Code Smell", "S3459:Unassigned members should be removed", Justification = "Propriété assignée par la désérialisation JSON")]
    private sealed class UnreadCountResponse
    {
        public int UnreadCount { get; init; }
    }
}