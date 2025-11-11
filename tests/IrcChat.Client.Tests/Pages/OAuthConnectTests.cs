// tests/IrcChat.Client.Tests/Pages/OAuthConnectTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class OAuthConnectTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IOAuthClientService> _oauthClientServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public OAuthConnectTests()
    {
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        _authServiceMock = new Mock<IUnifiedAuthService>();
        _oauthClientServiceMock = new Mock<IOAuthClientService>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_oauthClientServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void OAuthConnect_WithError_ShouldShowError()
    {
        // Arrange & Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["error"] = "access_denied"
        }));

        var cut = RenderComponent<OAuthConnect>();

        // Assert
        Assert.Contains("Erreur OAuth", cut.Markup);
        Assert.Contains("access_denied", cut.Markup);
    }

    [Fact]
    public void OAuthConnect_WithoutParams_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        RenderComponent<OAuthConnect>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task OAuthConnect_InitiateFlow_ShouldGenerateAuthUrl()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _oauthClientServiceMock
            .Setup(x => x.InitiateAuthorizationFlowAsync(
                ExternalAuthProvider.Google,
                It.IsAny<string>()))
            .ReturnsAsync("https://accounts.google.com/o/oauth2/auth?client_id=test");

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["provider"] = "Google",
            ["mode"] = "reserve"
        }));
        RenderComponent<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("google.com", _navManager.Uri);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_ShouldCompleteReservation()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var reserveResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "TestUser",
            Email = "test@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = true,
            IsAdmin = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(reserveResponse));

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state"
        }));

        RenderComponent<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
        _authServiceMock.Verify(
            x => x.SetAuthStateAsync(
                "test-token",
                "TestUser",
                "test@example.com",
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                ExternalAuthProvider.Google,
                false),
            Times.Once);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Login_ShouldComplete()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("login");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Microsoft");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var loginResponse = new OAuthLoginResponse
        {
            Token = "login-token",
            Username = "ExistingUser",
            Email = "existing@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = false,
            IsAdmin = true
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/login-reserved")
            .Respond(HttpStatusCode.OK, JsonContent.Create(loginResponse));

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_456",
            ["state"] = "xyz123"
        }));
        RenderComponent<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
        _authServiceMock.Verify(
            x => x.SetAuthStateAsync(
                "login-token",
                "ExistingUser",
                "existing@example.com",
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                ExternalAuthProvider.Microsoft,
                true),
            Times.Once);
    }

    [Fact]
    public void OAuthConnect_ShowsLoadingMessage()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _oauthClientServiceMock.Setup(x => x.InitiateAuthorizationFlowAsync(It.IsAny<ExternalAuthProvider>(), It.IsAny<string>()))
            .ReturnsAsync("https://example.com/auth");
        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["provider"] = "Google",
            ["mode"] = "reserve"
        }));

        var cut = RenderComponent<OAuthConnect>();

        // Assert
        Assert.Contains("Redirection vers Google...", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_ReserveFailure_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.BadRequest,
                new StringContent("{\"error\":\"username_taken\"}"));

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state"
        }));

        var cut = RenderComponent<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_LoginFailure_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("login");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/login-reserved")
            .Respond(HttpStatusCode.NotFound,
                new StringContent("{\"error\":\"not_found\"}"));

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_456",
            ["state"] = "state_xyz"
        }));
        var cut = RenderComponent<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_MissingSessionData_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult((string)null!); // Pas de username stocké

        // Act
        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state"
        }));

        var cut = RenderComponent<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("introuvable", cut.Markup);
    }
}