// tests/IrcChat.Client.Tests/Pages/SettingsTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class SettingsTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public SettingsTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void Settings_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act
        RenderComponent<Settings>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public void Settings_WithReservedUser_ShouldShowUserInfo()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.Email).Returns("test@example.com");
        _authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        _authServiceMock.Setup(x => x.AvatarUrl).Returns("https://example.com/avatar.jpg");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        Assert.Contains("TestUser", cut.Markup);
        Assert.Contains("test@example.com", cut.Markup);
        Assert.Contains("Google", cut.Markup);
        Assert.Contains("Pseudo réservé", cut.Markup);
    }

    [Fact]
    public void Settings_WithGuestUser_ShouldShowGuestBadge()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        Assert.Contains("GuestUser", cut.Markup);
        Assert.Contains("Invité", cut.Markup);
    }

    [Fact]
    public void Settings_WithAdminUser_ShouldShowAdminPanel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        _authServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _authServiceMock.Setup(x => x.Email).Returns("admin@example.com");

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        Assert.Contains("Administration", cut.Markup);
        Assert.Contains("⚡ Admin", cut.Markup);
    }

    [Fact]
    public async Task Settings_CreateChannel_WithReservedUser_ShouldSucceed()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        var createdChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow
        };

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = RenderComponent<Settings>();

        // Act
        var input = cut.Find(".input-group input");
        await cut.InvokeAsync(() => input.Input("test-channel"));

        var createButton = cut.Find(".input-group .btn-primary");
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        var count = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Settings_CreateChannel_DuplicateName_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.BadRequest,
                new StringContent("{\"error\":\"channel_exists\"}"));

        var cut = RenderComponent<Settings>();

        // Act
        var input = cut.Find(".input-group input");
        await cut.InvokeAsync(() => input.Input("existing-channel"));

        var createButton = cut.Find(".input-group .btn-primary");
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("existe déjà", cut.Markup);
    }

    [Fact]
    public void Settings_BackButton_ShouldNavigateToChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var cut = RenderComponent<Settings>();

        // Act
        var backButton = cut.Find(".back-button");
        cut.InvokeAsync(() => backButton.Click());

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
    }

    [Fact]
    public void Settings_GuestUser_ShouldShowReserveOption()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        Assert.Contains("réserver votre pseudo", cut.Markup);
        Assert.Contains("Réserver mon pseudo", cut.Markup);
    }

    [Fact]
    public async Task Settings_Logout_ShouldCallServiceAndRedirect()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        var cut = RenderComponent<Settings>();

        // Act
        var logoutButton = cut.Find("button:contains('Se déconnecter')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.LogoutAsync(), Times.Once);
        Assert.EndsWith("/login", _navManager.Uri);
    }
}