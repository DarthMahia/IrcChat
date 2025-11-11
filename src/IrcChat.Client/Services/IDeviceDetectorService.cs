// src/IrcChat.Client/Services/DeviceDetectorService.cs

namespace IrcChat.Client.Services;

public interface IDeviceDetectorService
{
    Task<int> GetScreenWidthAsync();
    Task<bool> IsMobileDeviceAsync();
}