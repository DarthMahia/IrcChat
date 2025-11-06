using Bunit;
using FluentAssertions;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class UnifiedAuthServiceTests : TestContext
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly LocalStorageService _localStorageService;
    private readonly Mock<HttpClient> _httpClientMock;

    public UnifiedAuthServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>(MockBehavior.Strict);
        _localStorageService = new LocalStorageService(_jsRuntimeMock.Object);
        _httpClientMock = new Mock<HttpClient>(MockBehavior.Strict);

        Services.AddSingleton(_localStorageService);
        Services.AddSingleton(_httpClientMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreFromLocalStorage()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = "test-token",
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            Email = "test@example.com",
            AvatarUrl = "https://example.com/avatar.jpg",
            UserId = Guid.NewGuid(),
            IsAdmin = false
        });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync(authData);

        var service = new UnifiedAuthService(_localStorageService, _httpClientMock.Object, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert
        service.HasUsername.Should().BeTrue();
        service.Username.Should().Be("testuser");
        service.IsAuthenticated.Should().BeTrue();
        service.IsReserved.Should().BeTrue();
        service.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task SetUsernameAsync_ShouldSaveToLocalStorage()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.Is<object[]>(o => o.Length == 2 && (string)o[0] == "ircchat_unified_auth" && o[1] is string)))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClientMock.Object, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var onAuthStateChangedCalled = false;
        service.OnAuthStateChanged += () => onAuthStateChangedCalled = true;

        // Act
        await service.SetUsernameAsync("newuser", isReserved: false);

        // Assert
        service.Username.Should().Be("newuser");
        service.IsReserved.Should().BeFalse();
        onAuthStateChangedCalled.Should().BeTrue();

        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.Is<object[]>(o => o.Length == 2 && (string)o[0] == "ircchat_unified_auth" && o[1] is string)),
            Times.Once);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearStateAndStorage()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = "test-token"
        });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync(authData);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClientMock.Object, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.ClearAllAsync();

        // Assert
        service.HasUsername.Should().BeFalse();
        service.Username.Should().BeNull();
        service.IsAuthenticated.Should().BeFalse();

        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")),
            Times.Once);
    }
}