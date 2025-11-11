/// <summary>
/// Constantes pour les noms de policies d'autorisation
/// </summary>

namespace IrcChat.Api.Authorization;

/// <summary>
/// Constantes pour les noms de policies d'autorisation
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy pour vérifier qu'un utilisateur peut modifier un canal.
    /// <para>
    /// <strong>Prérequis :</strong> Cette policy nécessite un paramètre de route nommé <c>channelName</c>.
    /// </para>
    /// <para>
    /// <strong>Autorisé si :</strong>
    /// <list type="bullet">
    /// <item>L'utilisateur est le créateur du canal, OU</item>
    /// <item>L'utilisateur est administrateur (IsAdmin = true)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note :</strong> Si le canal n'existe pas, la policy réussit pour permettre à l'endpoint 
    /// de retourner <c>404 NotFound</c> au lieu de <c>403 Forbidden</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// Utilisation dans un endpoint :
    /// <code>
    /// group.MapPost("/{channelName}/toggle-mute", ToggleMuteAsync)
    ///     .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
    ///     .WithName("ToggleChannelMute");
    /// </code>
    /// </example>
    public const string CanModifyChannel = "CanModifyChannel";
}