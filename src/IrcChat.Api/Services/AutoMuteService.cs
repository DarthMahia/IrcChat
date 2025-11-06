// src/IrcChat.Api/Services/AutoMuteService.cs
using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Services;

public class AutoMuteService(
    IDbContextFactory<ChatDbContext> dbContextFactory,
    IHubContext<ChatHub> hubContext,
    IOptions<AutoMuteOptions> options,
    ILogger<AutoMuteService> logger) : BackgroundService
{
    private readonly AutoMuteOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "AutoMuteService démarré - Inactivité: {InactivityMinutes}min, Vérification: {CheckInterval}s",
            _options.InactivityMinutes,
            _options.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndApplyAutoMute();
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.CheckIntervalSeconds),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la vérification auto-mute");
            }
        }
    }

    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "Can't be translated as SQL")]
    private async Task CheckAndApplyAutoMute()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        // Récupérer tous les canaux non mutés
        var activeChannels = await db.Channels
            .Where(c => !c.IsMuted)
            .ToListAsync();

        foreach (var channel in activeChannels)
        {
            // Déterminer qui doit être actif : le manager actif ou le créateur par défaut
            var managerUsername = channel.ActiveManager ?? channel.CreatedBy;

            // Vérifier si le manager est connecté et actif
            var managerConnection = await db.ConnectedUsers
                .Where(u => u.Username.ToLower() == managerUsername.ToLower())
                .OrderByDescending(u => u.LastPing)
                .FirstOrDefaultAsync();

            var shouldMute = false;

            if (managerConnection == null)
            {
                // Le manager n'est pas connecté du tout
                shouldMute = true;
            }
            else
            {
                // Vérifier l'inactivité du manager
                var inactiveThreshold = DateTime.UtcNow.AddMinutes(-_options.InactivityMinutes);
                if (managerConnection.LastPing < inactiveThreshold)
                {
                    shouldMute = true;
                }
            }

            if (shouldMute)
            {
                channel.IsMuted = true;
                await db.SaveChangesAsync();

                logger.LogInformation(
                    "Canal #{Channel} muté automatiquement (manager {Manager} inactif depuis {Minutes}min)",
                    channel.Name, managerUsername, _options.InactivityMinutes);

                // Notifier tous les utilisateurs du canal
                await hubContext.Clients.Group(channel.Name)
                    .SendAsync("ChannelMuteStatusChanged", channel.Name, true);
            }
        }
    }
}