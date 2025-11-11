// src/IrcChat.Api/Extensions/AdminManagementEndpoints.cs
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

public static class AdminManagementEndpoints
{
    public static WebApplication MapAdminManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin-management")
            .WithTags("Admin Management")
            .RequireAuthorization();

        // Obtenir la liste de tous les utilisateurs réservés
        group.MapGet("/users", GetUsersAsync)
        .WithName("GetAllReservedUsers")
        .WithOpenApi();

        // Promouvoir un utilisateur en admin
        group.MapPost("/{userId}/promote", PromoteAsAdminAsync)
        .WithName("PromoteUserToAdmin")
        .WithOpenApi();

        // Révoquer le statut d'admin
        group.MapPost("/{userId}/demote", DemoteAsync)
        .WithName("DemoteUserFromAdmin")
        .WithOpenApi();

        // Vérifier si l'utilisateur actuel est admin
        group.MapGet("/check-admin", CheckIsAdminAsync)
        .WithName("CheckAdminStatus")
        .WithOpenApi();

        return app;
    }

    private static async Task<IResult> CheckIsAdminAsync(ChatDbContext db, HttpContext context)
    {
        var currentUserId = GetCurrentUserId(context);
        if (currentUserId == null)
        {
            return Results.Ok(new { isAdmin = false });
        }

        var currentUser = await db.ReservedUsernames.FindAsync(currentUserId);
        return Results.Ok(new { isAdmin = currentUser?.IsAdmin ?? false });
    }

    private static async Task<IResult> DemoteAsync(Guid userId, ChatDbContext db, HttpContext context)
    {
        // Vérifier que l'utilisateur actuel est admin
        var currentUserId = GetCurrentUserId(context);
        if (currentUserId == null)
        {
            return Results.Unauthorized();
        }

        var currentUser = await db.ReservedUsernames.FindAsync(currentUserId);
        if (currentUser == null || !currentUser.IsAdmin)
        {
            return Results.Forbid();
        }

        // Empêcher un admin de se révoquer lui-même
        if (userId == currentUserId)
        {
            return Results.BadRequest(new { error = "cannot_demote_self" });
        }

        // Trouver l'utilisateur à révoquer
        var targetUser = await db.ReservedUsernames.FindAsync(userId);
        if (targetUser == null)
        {
            return Results.NotFound(new { error = "user_not_found" });
        }

        if (!targetUser.IsAdmin)
        {
            return Results.BadRequest(new { error = "not_admin" });
        }

        // Vérifier qu'il reste au moins un admin
        var adminCount = await db.ReservedUsernames.CountAsync(u => u.IsAdmin);
        if (adminCount <= 1)
        {
            return Results.BadRequest(new { error = "last_admin", message = "Impossible de révoquer le dernier administrateur" });
        }

        targetUser.IsAdmin = false;
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true, username = targetUser.Username });
    }

    private static async Task<IResult> PromoteAsAdminAsync(Guid userId, ChatDbContext db, HttpContext context)
    {
        // Vérifier que l'utilisateur actuel est admin
        var currentUserId = GetCurrentUserId(context);
        if (currentUserId == null)
        {
            return Results.Unauthorized();
        }

        var currentUser = await db.ReservedUsernames.FindAsync(currentUserId);
        if (currentUser == null || !currentUser.IsAdmin)
        {
            return Results.Forbid();
        }

        // Trouver l'utilisateur à promouvoir
        var targetUser = await db.ReservedUsernames.FindAsync(userId);
        if (targetUser == null)
        {
            return Results.NotFound(new { error = "user_not_found" });
        }

        if (targetUser.IsAdmin)
        {
            return Results.BadRequest(new { error = "already_admin" });
        }

        targetUser.IsAdmin = true;
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true, username = targetUser.Username });
    }

    private static async Task<IResult> GetUsersAsync(ChatDbContext db, HttpContext context)
    {
        // Vérifier que l'utilisateur est admin
        var currentUserId = GetCurrentUserId(context);
        if (currentUserId == null)
        {
            return Results.Unauthorized();
        }

        var currentUser = await db.ReservedUsernames.FindAsync(currentUserId);
        if (currentUser == null || !currentUser.IsAdmin)
        {
            return Results.Forbid();
        }

        var users = await db.ReservedUsernames
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.Provider,
                u.IsAdmin,
                u.CreatedAt,
                u.LastLoginAt,
                u.AvatarUrl
            })
            .ToListAsync();

        return Results.Ok(users);
    }

    private static Guid? GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}