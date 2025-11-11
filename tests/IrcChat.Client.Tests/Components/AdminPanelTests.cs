// tests/IrcChat.Client.Tests/Components/AdminPanelTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class AdminPanelTests : TestContext
{
    private readonly MockHttpMessageHandler _mockHttp;

    public AdminPanelTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void AdminPanel_OnInitialization_ShouldLoadUsers()
    {
        // Arrange
        var users = new List<object>
        {
            new
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Email = "user1@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = "https://example.com/avatar1.jpg"
            },
            new
            {
                Id = Guid.NewGuid(),
                Username = "admin1",
                Email = "admin1@test.com",
                Provider = ExternalAuthProvider.Microsoft,
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid()));

        // Assert
        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
        Assert.Contains("user1", cut.Markup);
        Assert.Contains("admin1", cut.Markup);
        Assert.Contains("user1@test.com", cut.Markup);
    }

    [Fact]
    public void AdminPanel_ShouldShowLoadingState()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(async () =>
            {
                await Task.Delay(100);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<object>())
                };
            });

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid()));

        // Assert
        Assert.Contains("Chargement", cut.Markup);
    }

    [Fact]
    public async Task AdminPanel_CloseButton_ShouldTriggerEvent()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<object>()));

        var closeTriggered = false;
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid())
            .Add(p => p.OnClose, () => closeTriggered = true));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Act
        var closeButton = cut.Find(".close-btn");
        await cut.InvokeAsync(() => closeButton.Click());

        // Assert
        Assert.True(closeTriggered);
    }

    [Fact]
    public async Task AdminPanel_PromoteUser_ShouldCallApiAndReloadUsers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var initialUsers = new List<object>
        {
            new
            {
                Id = userId,
                Username = "normalUser",
                Email = "user@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        var updatedUsers = new List<object>
        {
            new
            {
                Id = userId,
                Username = "normalUser",
                Email = "user@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        var getUsersRequest = _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/promote")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, currentUserId));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Reconfigurer pour retourner les users mis à jour
        _mockHttp.Clear();
        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/promote")
            .Respond(HttpStatusCode.OK);
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(updatedUsers));

        // Act
        var promoteButton = cut.Find(".btn-action.promote");
        await cut.InvokeAsync(() => promoteButton.Click());
        await Task.Delay(300);

        // Assert
        Assert.Contains("promu administrateur", cut.Markup);
        Assert.Contains("⚡ Admin", cut.Markup);
    }

    [Fact]
    public async Task AdminPanel_DemoteUser_ShouldCallApiAndReloadUsers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var initialUsers = new List<object>
        {
            new
            {
                Id = userId,
                Username = "adminUser",
                Email = "admin@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        var updatedUsers = new List<object>
        {
            new
            {
                Id = userId,
                Username = "adminUser",
                Email = "admin@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/demote")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, currentUserId));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
        cut.Render();

        _mockHttp.Clear();
        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/demote")
            .Respond(HttpStatusCode.OK);
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(updatedUsers));

        // Act
        var demoteButton = cut.Find(".btn-action.demote");
        await cut.InvokeAsync(() => demoteButton.Click());
        await Task.Delay(300);

        // Assert
        Assert.Contains("révoqué", cut.Markup);
    }

    [Fact]
    public async Task AdminPanel_PromoteUser_OnError_ShouldShowError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var users = new List<object>
        {
            new
            {
                Id = userId,
                Username = "normalUser",
                Email = "user@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/promote")
            .Respond(HttpStatusCode.Forbidden, new StringContent("Access denied"));

        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, currentUserId));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Act
        var promoteButton = cut.Find(".btn-action.promote");
        await cut.InvokeAsync(() => promoteButton.Click());
        await Task.Delay(300);

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public void AdminPanel_CurrentUser_ShouldNotShowActions()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();

        var users = new List<object>
        {
            new
            {
                Id = currentUserId,
                Username = "currentUser",
                Email = "current@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, currentUserId));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("(Vous)", cut.Markup);
        Assert.Empty(cut.FindAll(".btn-action"));
    }

    [Fact]
    public void AdminPanel_LoadUsers_OnError_ShouldShowError()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid()));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public void AdminPanel_ShouldDisplayProviderBadge()
    {
        // Arrange
        var users = new List<object>
        {
            new
            {
                Id = Guid.NewGuid(),
                Username = "googleUser",
                Email = "google@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid()));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("Google", cut.Markup);
        Assert.NotNull(cut.Find(".provider-badge"));
    }

    [Fact]
    public void AdminPanel_WithAvatar_ShouldDisplayImage()
    {
        // Arrange
        var users = new List<object>
        {
            new
            {
                Id = Guid.NewGuid(),
                Username = "userWithAvatar",
                Email = "avatar@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = "https://example.com/avatar.jpg"
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        // Act
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid()));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Assert
        var avatar = cut.Find(".user-avatar");
        Assert.NotNull(avatar);
        Assert.Equal("https://example.com/avatar.jpg", avatar.GetAttribute("src"));
    }

    [Fact]
    public async Task AdminPanel_WhileProcessing_ShouldDisableButtons()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var users = new List<object>
        {
            new
            {
                Id = userId,
                Username = "normalUser",
                Email = "user@test.com",
                Provider = ExternalAuthProvider.Google,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                AvatarUrl = (string?)null
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        _mockHttp.When(HttpMethod.Post, $"*/api/admin-management/{userId}/promote")
            .Respond(async () =>
            {
                await Task.Delay(1000);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, currentUserId));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Act
        var promoteButton = cut.Find(".btn-action.promote");
        await cut.InvokeAsync(() => promoteButton.Click());

        // Assert
        Assert.True(promoteButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task AdminPanel_OverlayClick_ShouldClose()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/admin-management/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<object>()));

        var closeTriggered = false;
        var cut = RenderComponent<AdminPanel>(parameters => parameters
            .Add(p => p.CurrentUserId, Guid.NewGuid())
            .Add(p => p.OnClose, () => closeTriggered = true));

        cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

        // Act
        var overlay = cut.Find(".admin-panel-overlay");
        await cut.InvokeAsync(() => overlay.Click());

        // Assert
        Assert.True(closeTriggered);
    }
}