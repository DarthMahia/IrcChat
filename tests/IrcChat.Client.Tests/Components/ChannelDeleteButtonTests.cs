// tests/IrcChat.Client.Tests/Components/ChannelDeleteButtonTests.cs
using System.Net;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelDeleteButtonTests : TestContext
{
    private readonly MockHttpMessageHandler _mockHttp;

    public ChannelDeleteButtonTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChannelDeleteButton_WhenCannotManage_ShouldNotShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, false));

        // Assert
        cut.FindAll(".delete-btn").Should().BeEmpty();
    }

    [Fact]
    public void ChannelDeleteButton_WhenCanManage_ShouldShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        // Assert
        var button = cut.Find(".delete-btn");
        button.Should().NotBeNull();
        button.TextContent.Should().Contain("Supprimer le salon");
    }

    [Fact]
    public async Task ChannelDeleteButton_OnClick_ShouldShowConfirmation()
    {
        // Arrange
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        cut.Find(".modal-overlay").Should().NotBeNull();
        cut.Markup.Should().Contain("Confirmer la suppression");
        cut.Markup.Should().Contain("#general");
    }

    [Fact]
    public async Task ChannelDeleteButton_OnCancel_ShouldHideConfirmation()
    {
        // Arrange
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Act
        var cancelButton = cut.Find(".btn-secondary");
        await cut.InvokeAsync(() => cancelButton.Click());

        // Assert
        cut.FindAll(".modal-overlay").Should().BeEmpty();
    }

    [Fact]
    public async Task ChannelDeleteButton_OnConfirm_ShouldCallApi()
    {
        // Arrange
        var mockedRequest = _mockHttp.When(HttpMethod.Delete, "*/api/channels/general")
            .Respond(HttpStatusCode.OK);

        var eventTriggered = false;
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnChannelDeleted, (string channel) => eventTriggered = true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());
        cut.Render();

        // Act
        var confirmButton = cut.Find(".btn-danger");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(200);

        // Assert
        _mockHttp.GetMatchCount(mockedRequest)
            .Should().Be(1);
        eventTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task ChannelDeleteButton_OnError_ShouldShowErrorMessage()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/channels/general")
            .Respond(HttpStatusCode.Forbidden);

        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Act
        var confirmButton = cut.Find(".btn-danger");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(200);
        cut.Render();
        // Assert
        cut.Markup.Should().Contain("Erreur");
    }

    [Fact]
    public async Task ChannelDeleteButton_WhileProcessing_ShouldDisableButton()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/channels/general")
            .Respond(async () =>
            {
                await Task.Delay(1000);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Act
        var confirmButton = cut.Find(".btn-danger");
        await cut.InvokeAsync(() => confirmButton.Click());

        // Assert
        confirmButton.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ChannelDeleteButton_ModalWarning_ShouldBeVisible()
    {
        // Arrange
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        // Act
        var deleteButton = cut.Find(".delete-btn");
        cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        cut.Markup.Should().Contain("irréversible");
        cut.Markup.Should().Contain("messages");
    }

    [Fact]
    public async Task ChannelDeleteButton_OnModalBackdropClick_ShouldClose()
    {
        // Arrange
        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CanManage, true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Act
        var backdrop = cut.Find(".modal-overlay");
        await cut.InvokeAsync(() => backdrop.Click());

        // Assert
        cut.FindAll(".modal-overlay").Should().BeEmpty();
    }

    [Fact]
    public async Task ChannelDeleteButton_WithEmptyChannelName_ShouldNotCallApi()
    {
        // Arrange
        var mockedRequest = _mockHttp.When(HttpMethod.Delete, "*/api/channels/*")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<ChannelDeleteButton>(parameters => parameters
            .Add(p => p.ChannelName, "")
            .Add(p => p.CanManage, true));

        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Act
        var confirmButton = cut.Find(".btn-danger");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(200);

        // Assert
        _mockHttp.GetMatchCount(mockedRequest)
            .Should().Be(0);
    }
}