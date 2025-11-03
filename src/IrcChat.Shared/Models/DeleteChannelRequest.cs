// src/IrcChat.Shared/Models/DeleteChannelRequest.cs
namespace IrcChat.Shared.Models;

public class DeleteChannelRequest
{
    public string ChannelName { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
}