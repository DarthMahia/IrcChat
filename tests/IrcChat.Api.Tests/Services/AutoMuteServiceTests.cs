using FluentAssertions;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class AutoMuteServiceTests : IAsyncDisposable
{
    private readonly PooledDbContextFactory<ChatDbContext> _dbContextFactory;
    private readonly Mock<ILogger<AutoMuteService>> _loggerMock;
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly IOptions<AutoMuteOptions> _options;

    public AutoMuteServiceTests()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());

        _dbContextFactory = new PooledDbContextFactory<ChatDbContext>(optionsBuilder.Options);

        _loggerMock = new Mock<ILogger<AutoMuteService>>();
        _hubContextMock = new Mock<IHubContext<ChatHub>>();

        // Configuration correcte du mock pour la chaîne Clients.Group(...).SendAsync(...)
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        // Setup pour Group() qui retourne le ClientProxy
        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);

        // Setup pour All si besoin
        mockClients
            .Setup(c => c.All)
            .Returns(mockClientProxy.Object);

        _hubContextMock
            .Setup(h => h.Clients)
            .Returns(mockClients.Object);

        _options = Options.Create(new AutoMuteOptions
        {
            CheckIntervalSeconds = 1,
            InactivityMinutes = 5
        });
    }

    [Fact]
    public async Task AutoMuteService_ShouldMuteChannelWhenOwnerInactive()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // Canal avec propriétaire inactif
        var inactiveChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "inactive-channel",
            CreatedBy = "inactive_user",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        // Propriétaire connecté mais inactif (dernier ping > 5 minutes)
        var inactiveOwner = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "inactive_user",
            ConnectionId = "conn_1",
            Channel = inactiveChannel.Name,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-10), // Inactif depuis 10 minutes
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-server"
        };

        context.Channels.Add(inactiveChannel);
        context.ConnectedUsers.Add(inactiveOwner);
        await context.SaveChangesAsync();

        var service = new AutoMuteService(
            _dbContextFactory,
            _hubContextMock.Object,
            _options,
            _loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2)); // Attendre le check
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == inactiveChannel.Name);

        result.Should().NotBeNull();
        result!.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task AutoMuteService_ShouldMuteChannelWhenOwnerDisconnected()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // Canal dont le propriétaire n'est pas connecté
        var abandonedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "abandoned-channel",
            CreatedBy = "disconnected_user",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        context.Channels.Add(abandonedChannel);
        await context.SaveChangesAsync();

        var service = new AutoMuteService(
            _dbContextFactory,
            _hubContextMock.Object,
            _options,
            _loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == abandonedChannel.Name);

        result.Should().NotBeNull();
        result!.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task AutoMuteService_ShouldNotMuteChannelWhenOwnerActive()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var activeChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "active-channel",
            CreatedBy = "active_user",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        // Propriétaire actif (dernier ping récent)
        var activeOwner = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            ConnectionId = "conn_1",
            Channel = activeChannel.Name,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-2),
            LastPing = DateTime.UtcNow.AddSeconds(-30), // Actif il y a 30 secondes
            LastActivity = DateTime.UtcNow.AddSeconds(-30),
            ServerInstanceId = "test-server"
        };

        context.Channels.Add(activeChannel);
        context.ConnectedUsers.Add(activeOwner);
        await context.SaveChangesAsync();

        var service = new AutoMuteService(
            _dbContextFactory,
            _hubContextMock.Object,
            _options,
            _loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == activeChannel.Name);

        result.Should().NotBeNull();
        result!.IsMuted.Should().BeFalse();
    }

    [Fact]
    public async Task AutoMuteService_ShouldNotMuteAlreadyMutedChannels()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "already-muted",
            CreatedBy = "user1",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = true // Déjà muté
        };

        context.Channels.Add(mutedChannel);
        await context.SaveChangesAsync();

        var service = new AutoMuteService(
            _dbContextFactory,
            _hubContextMock.Object,
            _options,
            _loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == mutedChannel.Name);

        result.Should().NotBeNull();
        result!.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task AutoMuteService_ShouldHandleMultipleChannels()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var activeChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "active",
            CreatedBy = "active_user",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        var inactiveChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "inactive",
            CreatedBy = "inactive_user",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        var activeOwner = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            ConnectionId = "conn_1",
            Channel = activeChannel.Name,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        var inactiveOwner = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "inactive_user",
            ConnectionId = "conn_2",
            Channel = inactiveChannel.Name,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-server"
        };

        context.Channels.AddRange(activeChannel, inactiveChannel);
        context.ConnectedUsers.AddRange(activeOwner, inactiveOwner);
        await context.SaveChangesAsync();

        var service = new AutoMuteService(
            _dbContextFactory,
            _hubContextMock.Object,
            _options,
            _loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();

        var activeResult = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "active");
        activeResult.Should().NotBeNull();
        activeResult!.IsMuted.Should().BeFalse();

        var inactiveResult = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "inactive");
        inactiveResult.Should().NotBeNull();
        inactiveResult!.IsMuted.Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}