using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Authorization;

/// <summary>
/// Requirement pour vérifier qu'un utilisateur peut modifier un canal
/// (créateur du canal ou admin)
/// </summary>
public class ChannelModificationRequirement(string channelName) : IAuthorizationRequirement
{
    public string ChannelName { get; } = channelName;
}