using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Api.Endpoints;
using IrcChat.Api.Hubs;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

public static class WebApplicationExtensions
{
    [SuppressMessage("SonarAnalyzer", "S2139", Justification = "Already log and rethrow correctly")]
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }

            logger.LogInformation("✅ Base de données PostgreSQL migrée avec succès");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Erreur lors de l'initialisation de la base de données");
            throw;
        }

        return app;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "IRC Chat API v1");
                options.RoutePrefix = "swagger";
            });
        }

        app.UseCors("AllowBlazor");
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app) =>
        app.MapOAuthEndpoints()
           .MapMessageEndpoints()
           .MapChannelEndpoints()
           .MapChannelMuteEndpoints()
           .MapChannelDeleteEndpoints()
           .MapPrivateMessageEndpoints()
           .MapAdminManagementEndpoints()
           .MapSignalRHub();

    private static WebApplication MapSignalRHub(this WebApplication app)
    {
        app.MapHub<ChatHub>("/chathub");
        return app;
    }
}