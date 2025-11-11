// src/IrcChat.Client/Services/DeviceDetectorService.cs
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class DeviceDetectorService(IJSRuntime jsRuntime, ILogger<DeviceDetectorService> logger) : IDeviceDetectorService, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _isInitialized;

    public async Task<bool> IsMobileDeviceAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            return await _module!.InvokeAsync<bool>("isMobileDevice");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la détection du type d'appareil");
            return false;
        }
    }

    public async Task<int> GetScreenWidthAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            return await _module!.InvokeAsync<int>("getScreenWidth");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la récupération de la largeur d'écran");
            return 1024; // Valeur par défaut (desktop)
        }
    }

    [SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "It's logged")]
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            _module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/device-detector.js");
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement du module de détection d'appareil");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors de la libération du module de détection d'appareil");
            }
        }

        GC.SuppressFinalize(this);
    }
}