// tests/IrcChat.Client.Tests/Components/ChannelMuteButtonTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelMuteButtonTests : TestContext
{
    private readonly MockHttpMessageHandler _mockHttp;

    public ChannelMuteButtonTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChannelMuteButton_WhenCannotManage_ShouldNotShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Empty(cut.FindAll(".mute-btn"));
    }

    [Fact]
    public void ChannelMuteButton_WhenCanManage_ShouldShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Assert
        var button = cut.Find(".mute-btn");
        Assert.NotNull(button);
        Assert.Contains("Muter le salon", button.TextContent);
    }

    [Fact]
    public void ChannelMuteButton_WhenMuted_ShouldShowUnmuteButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, true)
            .Add(p => p.CanManage, true));

        // Assert
        var button = cut.Find(".mute-btn");
        Assert.Contains("Salon muet", button.TextContent);
        Assert.Contains("muted", button.ClassList);
    }

    [Fact]
    public void ChannelMuteButton_WhenCannotManageButMuted_ShouldShowIndicator()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, true)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Empty(cut.FindAll(".mute-btn"));
        var indicator = cut.Find(".muted-indicator");
        Assert.NotNull(indicator);
        Assert.Contains("Salon muet", indicator.TextContent);
    }

    [Fact]
    public async Task ToggleMute_ShouldCallApiAndUpdateStatus()
    {
        // Arrange
        var muteToggleResponse = new { ChannelName = "general", IsMuted = true, ChangedBy = "admin" };

        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.OK, JsonContent.Create(muteToggleResponse));

        var statusChanged = false;
        var newStatus = false;

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true)
            .Add(p => p.OnMuteStatusChanged, (bool isMuted) =>
            {
                statusChanged = true;
                newStatus = isMuted;
            }));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);

        // Assert
        Assert.True(statusChanged);
        Assert.True(newStatus);
    }

    [Fact]
    public async Task ToggleMute_OnError_ShouldShowErrorMessage()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.Forbidden);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public async Task ToggleMute_WhileProcessing_ShouldDisableButton()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(async () =>
            {
                await Task.Delay(1000);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task ToggleMute_WithEmptyChannelName_ShouldNotCallApi()
    {
        // Arrange
        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels/*/toggle-mute")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);

        // Assert
        Assert.Equal(0, _mockHttp.GetMatchCount(mockedRequest));
    }

    [Fact]
    public async Task ToggleMute_ErrorMessageShouldDisappear()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.BadRequest);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);
        cut.Render();

        Assert.Contains("Erreur", cut.Markup);

        // Attendre que le message disparaisse
        await Task.Delay(3200);
        cut.Render();

        // Assert
        Assert.DoesNotContain("Erreur", cut.Markup);
    }

    [Fact]
    public async Task ToggleMute_ShouldNotTriggerWhenAlreadyProcessing()
    {
        // Arrange
        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute");
        mockedRequest.Respond(async () =>
        {
            await Task.Delay(500);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ChannelName = "general", IsMuted = true, ChangedBy = "admin" })
            };
        });

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        var button = cut.Find(".mute-btn");

        // Act - Double click rapide
        await cut.InvokeAsync(() => button.Click());
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(600);
        cut.Render();

        // Assert - Devrait être appelé une seule fois
        var requestCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, requestCount);
    }
}