using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Authorization;

/// <summary>
/// Handler pour vérifier qu'un utilisateur peut modifier un canal
/// Note: Ce handler vérifie uniquement les PERMISSIONS, pas l'existence de la ressource.
/// Si le canal n'existe pas, le handler réussit pour laisser l'endpoint retourner NotFound.
/// </summary>
[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public class ChannelModificationHandler(ChatDbContext db, ILogger<ChannelModificationHandler> logger)
    : AuthorizationHandler<ChannelModificationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ChannelModificationRequirement requirement)
    {
        // Récupérer le username depuis les claims
        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(username))
        {
            logger.LogWarning("Tentative de modification de canal sans username dans les claims");
            context.Fail();
            return;
        }

        // Vérifier que le canal existe
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == requirement.ChannelName.ToLower());

        if (channel == null)
        {
            // Réussir l'autorisation pour un canal inexistant
            // L'endpoint vérifiera l'existence et retournera NotFound
            logger.LogDebug("Canal {ChannelName} introuvable lors de la vérification d'autorisation, autorisation accordée pour permettre NotFound",
                requirement.ChannelName);
            context.Succeed(requirement);
            return;
        }

        // Vérifier si l'utilisateur est le créateur
        var isCreator = channel.CreatedBy.Equals(username, StringComparison.OrdinalIgnoreCase);

        if (isCreator)
        {
            logger.LogInformation("Utilisateur {Username} autorisé en tant que créateur du canal {ChannelName}",
                username, channel!.Name);
            context.Succeed(requirement);
            return;
        }

        // Vérifier si l'utilisateur est admin
        var user = await db.ReservedUsernames
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user?.IsAdmin == true)
        {
            logger.LogInformation("Utilisateur {Username} autorisé en tant qu'admin pour le canal {ChannelName}",
                username, channel.Name);
            context.Succeed(requirement);
            return;
        }

        logger.LogWarning("Utilisateur {Username} non autorisé à modifier le canal {ChannelName}",
            username, channel.Name);
        context.Fail();
    }
}