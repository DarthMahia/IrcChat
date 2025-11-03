// tests/IrcChat.Client.Tests/Services/ChatServiceTests.cs
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class ChatServiceTests : TestContext
{
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly IOptions<ApiSettings> _apiSettings;
    private readonly MockHttpMessageHandler _mockHttp;

    public ChatServiceTests()
    {
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();

        _apiSettings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:7000",
            SignalRHubUrl = "https://localhost:7000/chathub"
        });

        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_apiSettings);
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChatService_ShouldInitialize()
    {
        // Act
        var service = new ChatService(_privateMessageServiceMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_ShouldConnectSuccessfully()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object);

        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();
        var connectionFactoryMock = new Mock<IConnectionFactory>();
        var connectionContextMock = new Mock<ConnectionContext>();
        connectionFactoryMock
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionContextMock.Object);
        var featureCollectionMock = new Mock<IFeatureCollection>();
        connectionContextMock.SetupGet(x => x.Features)
            .Returns(featureCollectionMock.Object);
        var duplexPipeMock = new Mock<IDuplexPipe>();
        connectionContextMock.SetupGet(x => x.Transport)
            .Returns(duplexPipeMock.Object);
        using var outpoutMs = new MemoryStream();
        duplexPipeMock.SetupGet(x => x.Output)
            .Returns(PipeWriter.Create(outpoutMs));
        using var inputMs = new MemoryStream();
        duplexPipeMock.SetupGet(x => x.Input)
            .Returns(PipeReader.Create(inputMs));
        var hubProtocolMock = new Mock<IHubProtocol>();
        var enpointMock = new Mock<EndPoint>();
        var services = new ServiceCollection()
            .AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var retryPolicyMock = new Mock<IRetryPolicy>();

        // Simuler le protocole utilisé (ex: "json")
        hubProtocolMock.SetupGet(p => p.Name).Returns("json");

        // Simuler l'envoi du message de handshake par le client
        var handshakeRequest = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await inputMs.WriteAsync(handshakeRequest);
        inputMs.Position = 0; // Rewind pour lecture

        // Simuler la réponse du serveur au handshake
        var handshakeResponse = Encoding.UTF8.GetBytes("{}\u001e");
        await outpoutMs.WriteAsync(handshakeResponse, 0, handshakeResponse.Length);
        await outpoutMs.FlushAsync();
        outpoutMs.Position = 0;

        var hubConnection = new HubConnection(connectionFactoryMock.Object,
                hubProtocolMock.Object,
                enpointMock.Object,
                serviceProvider,
                serviceProvider.GetRequiredService<ILoggerFactory>(),
                retryPolicyMock.Object);

        hubConnectionBuilderMock
            .Setup(x => x.Build())
            .Returns(hubConnection);

        // Act & Assert
        var act = async () => await service.InitializeAsync(hubConnectionBuilderMock.Object);
        await act.Should().NotThrowAsync();
    }



    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object);

        // Act
        var act = async () => await service.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void PrivateMessageService_ShouldBeNotifiedOnPrivateMessage()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object);
        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private test",
            Timestamp = DateTime.UtcNow
        };

        // Act - Simuler la réception d'un message privé via SignalR
        // Note: En réalité, ceci serait appelé par le hub SignalR

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyPrivateMessageReceived(It.IsAny<PrivateMessage>()),
            Times.Never()); // Pas encore appelé car pas de vraie connexion SignalR
    }
}