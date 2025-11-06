// Dans IrcChat.Shared/Models/Channel.cs
namespace IrcChat.Shared.Models;

public class Channel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMuted { get; set; }

    /// <summary>
    /// Utilisateur qui gère actuellement le salon (pour l'auto-mute).
    /// Par défaut, c'est le créateur. Un admin peut prendre le relais en démutant.
    /// </summary>
    public string? ActiveManager { get; set; }
}