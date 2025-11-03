using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IrcChat.Api.Extensions;

public static class OAuthEndpoints
{
    [SuppressMessage("Performance", "CA1862", Justification = "Not translated in SQL requests")]
    public static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var oauth = app.MapGroup("/api/oauth")
            .WithTags("OAuth");

        oauth.MapPost("/check-username", CheckUsernameAsync)
            .WithName("CheckUsername")
            .WithOpenApi();

        oauth.MapPost("/reserve-username", ReserveUsernameAsync)
            .WithName("ReserveUsername")
            .WithOpenApi();

        oauth.MapPost("/login-reserved", LoginReservedAsync)
            .WithName("LoginReserved")
            .WithOpenApi();

        oauth.MapGet("/config/{provider}", GetProviderConfigAsync)
            .WithName("GetProviderConfig")
            .WithOpenApi();

        oauth.MapPost("/forget-username", ForgetUsernameAsync)
            .RequireAuthorization()
            .WithName("ForgetUsername")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> CheckUsernameAsync(
        UsernameCheckRequest request,
        ChatDbContext context)
    {
        var username = request.Username.ToLower();

        var reservedUser = await context.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Username.ToLower() == username);

        var currentlyUsed = await context.ConnectedUsers
            .AnyAsync(u => u.Username.ToLower() == username);

        return Results.Ok(new UsernameCheckResponse
        {
            Available = reservedUser == null && !currentlyUsed,
            IsReserved = reservedUser != null,
            ReservedProvider = reservedUser?.Provider,
            IsCurrentlyUsed = currentlyUsed
        });
    }

    private static async Task<IResult> ReserveUsernameAsync(
        ReserveUsernameRequest request,
        ChatDbContext context,
        OAuthService oauthService,
        IConfiguration configuration)
    {
        var exists = await context.ReservedUsernames
            .AnyAsync(r => r.Username.ToLower() == request.Username.ToLower());

        if (exists)
        {
            return Results.BadRequest(new { error = "username_taken", message = "Ce pseudo est déjà réservé" });
        }

        var tokenResponse = await oauthService.ExchangeCodeForTokenAsync(
            request.Provider,
            request.Code,
            request.RedirectUri,
            request.CodeVerifier);

        if (tokenResponse == null)
        {
            return Results.Unauthorized();
        }

        var userInfo = await oauthService.GetUserInfoAsync(request.Provider, tokenResponse.AccessToken);

        if (userInfo == null)
        {
            return Results.Unauthorized();
        }

        var existingUser = await context.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Provider == request.Provider && r.ExternalUserId == userInfo.Id);

        if (existingUser != null)
        {
            return Results.BadRequest(new
            {
                error = "already_reserved",
                message = "Vous avez déjà un pseudo réservé",
                username = existingUser.Username
            });
        }

        var isFirstUser = !await context.ReservedUsernames.AnyAsync();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Provider = request.Provider,
            ExternalUserId = userInfo.Id,
            Email = userInfo.Email,
            DisplayName = userInfo.Name,
            AvatarUrl = userInfo.AvatarUrl,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = isFirstUser
        };

        context.ReservedUsernames.Add(user);
        await context.SaveChangesAsync();

        var token = GenerateJwtToken(user, configuration);

        return Results.Ok(new OAuthLoginResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            UserId = user.Id,
            IsNewUser = true,
            IsAdmin = user.IsAdmin
        });
    }

    private static async Task<IResult> LoginReservedAsync(
        OAuthTokenRequest request,
        ChatDbContext context,
        OAuthService oauthService,
        IConfiguration configuration)
    {
        var tokenResponse = await oauthService.ExchangeCodeForTokenAsync(
            request.Provider,
            request.Code,
            request.RedirectUri,
            request.CodeVerifier);

        if (tokenResponse == null)
        {
            return Results.Unauthorized();
        }

        var userInfo = await oauthService.GetUserInfoAsync(request.Provider, tokenResponse.AccessToken);

        if (userInfo == null)
        {
            return Results.Unauthorized();
        }

        var user = await context.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Provider == request.Provider && r.ExternalUserId == userInfo.Id);

        if (user == null)
        {
            return Results.NotFound(new { error = "not_found", message = "Aucun pseudo réservé trouvé" });
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.AvatarUrl = userInfo.AvatarUrl;
        await context.SaveChangesAsync();

        var token = GenerateJwtToken(user, configuration);

        return Results.Ok(new OAuthLoginResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            UserId = user.Id,
            IsNewUser = false,
            IsAdmin = user.IsAdmin
        });
    }

    private static IResult GetProviderConfigAsync(
        ExternalAuthProvider provider,
        OAuthService oauthService)
    {
        try
        {
            var config = oauthService.GetProviderConfig(provider);
            return Results.Ok(new
            {
                provider,
                config.AuthorizationEndpoint,
                config.ClientId,
                config.Scope
            });
        }
        catch
        {
            return Results.BadRequest("Provider not supported");
        }
    }

    private static async Task<IResult> ForgetUsernameAsync(
        HttpContext context,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        var usernameClaim = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name);

        var username = usernameClaim?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Results.Unauthorized();
        }

        var userToDelete = await db.ReservedUsernames
            .FirstOrDefaultAsync(r => r.Username.ToLower() == username);

        if (userToDelete == null)
        {
            return Results.NotFound();
        }

        db.ReservedUsernames.Remove(userToDelete);
        logger.LogInformation("Utilisateur réservé {Username} supprimé de la BDD.", username);

        await db.SaveChangesAsync();

        return Results.Ok();
    }

    [SuppressMessage("SonarAnalyzer", "S6781", Justification = "Use env var to configure it")]
    private static string GenerateJwtToken(ReservedUsername user, IConfiguration configuration)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("provider", user.Provider.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}