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

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);

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
    public async Task AutoMuteService_ShouldMuteChannelWhenActiveManagerInactive()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var inactiveChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "inactive-channel",
            CreatedBy = "original_creator",
            ActiveManager = "inactive_manager", // Manager différent du créateur
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        // Manager inactif (dernier ping superieur à 5 minutes)
        var inactiveManager = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "inactive_manager",
            ConnectionId = "conn_1",
            Channel = inactiveChannel.Name,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-server"
        };

        context.Channels.Add(inactiveChannel);
        context.ConnectedUsers.Add(inactiveManager);
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
            .FirstOrDefaultAsync(c => c.Name == inactiveChannel.Name);

        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
    }

    [Fact]
    public async Task AutoMuteService_ShouldMuteWhenActiveManagerDisconnected()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var abandonedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "abandoned-channel",
            CreatedBy = "creator",
            ActiveManager = "disconnected_admin", // Admin qui n'est plus connecté
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

        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
    }

    [Fact]
    public async Task AutoMuteService_ShouldNotMuteWhenActiveManagerActive()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var activeChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "active-channel",
            CreatedBy = "creator",
            ActiveManager = "active_admin", // Un admin actif gère le salon
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        var activeManager = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_admin",
            ConnectionId = "conn_1",
            Channel = activeChannel.Name,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-2),
            LastPing = DateTime.UtcNow.AddSeconds(-30), // Actif
            LastActivity = DateTime.UtcNow.AddSeconds(-30),
            ServerInstanceId = "test-server"
        };

        context.Channels.Add(activeChannel);
        context.ConnectedUsers.Add(activeManager);
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

        Assert.NotNull(result);
        Assert.False(result!.IsMuted);
    }

    [Fact]
    public async Task AutoMuteService_ShouldUseCreatorWhenActiveManagerIsNull()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "no-manager-channel",
            CreatedBy = "creator",
            ActiveManager = null, // Pas de manager actif défini
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = false
        };

        // Le créateur n'est pas connecté
        context.Channels.Add(channel);
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

        // Assert - Devrait muter car le créateur (fallback) n'est pas connecté
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == channel.Name);

        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
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
            ActiveManager = "user1",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsMuted = true
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

        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
    }

    [Fact]
    public async Task AutoMuteService_ShouldHandleMultipleChannelsWithDifferentManagers()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // Canal 1: Manager actif
        var activeChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "active",
            CreatedBy = "creator1",
            ActiveManager = "active_admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        var activeManager = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_admin",
            ConnectionId = "conn_1",
            Channel = activeChannel.Name,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        // Canal 2: Manager inactif
        var inactiveChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "inactive",
            CreatedBy = "creator2",
            ActiveManager = "inactive_user",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        var inactiveManager = new ConnectedUser
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
        context.ConnectedUsers.AddRange(activeManager, inactiveManager);
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
        Assert.NotNull(activeResult);
        Assert.False(activeResult!.IsMuted);

        var inactiveResult = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "inactive");
        Assert.NotNull(inactiveResult);
        Assert.True(inactiveResult!.IsMuted);
    }

    [Fact]
    public async Task AutoMuteService_CreatorActiveButNotManager_ShouldMute()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "admin-managed",
            CreatedBy = "creator",
            ActiveManager = "admin", // Un admin gère, pas le créateur
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };

        // Le créateur est actif mais n'est plus le manager
        var activeCreator = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            ConnectionId = "conn_1",
            Channel = channel.Name,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        };

        // L'admin (manager actif) n'est pas connecté
        context.Channels.Add(channel);
        context.ConnectedUsers.Add(activeCreator);
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

        // Assert - Devrait muter car le manager actif (admin) n'est pas connecté
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var result = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == channel.Name);

        Assert.NotNull(result);
        Assert.True(result!.IsMuted);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}