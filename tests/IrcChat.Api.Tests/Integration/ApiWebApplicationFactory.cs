// tests/IrcChat.Api.Tests/Integration/ApiWebApplicationFactory.cs
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace IrcChat.Api.Tests.Integration;

public class ApiWebApplicationFactory : WebApplicationFactory<ChatHub>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    => builder.ConfigureTestServices(services =>
            {
                // Remplacer le DbContext par une version InMemory pour les tests
                services.RemoveAll<IDbContextOptionsConfiguration<ChatDbContext>>()
                    .RemoveAll<IDbContextFactory<ChatDbContext>>()
                    .RemoveAll<DbContextOptions<ChatDbContext>>()
                    .RemoveAll<ChatDbContext>();

                var dbId = Guid.NewGuid().ToString(); // Utiliser une base de données unique par instance
                services.AddDbContext<ChatDbContext>(options => options.UseInMemoryDatabase(dbId))
                    .AddSingleton<IDbContextFactory<ChatDbContext>>(sp =>
                    {
                        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
                        optionsBuilder.UseInMemoryDatabase(dbId);
                        return new PooledDbContextFactory<ChatDbContext>(optionsBuilder.Options);
                    });

                services.RemoveAll<OAuthService>();
                services.AddScoped<OAuthService>(sp =>
                {
                    var mock = new Mock<OAuthServiceStub>();

                    // Cas nominal
                    mock.Setup(x => x.ExchangeCodeForTokenAsync(It.IsAny<ExternalAuthProvider>(), "valid_code", It.IsAny<string>(), It.IsAny<string>()))
                        .ReturnsAsync(new OAuthTokenResponse { AccessToken = "token" });

                    mock.Setup(x => x.GetUserInfoAsync(It.IsAny<ExternalAuthProvider>(), "token"))
                        .ReturnsAsync(new ExternalUserInfo { Id = "google-123", Name = "google-123" });

                    // Cas échec
                    mock.Setup(x => x.ExchangeCodeForTokenAsync(It.IsAny<ExternalAuthProvider>(), "invalid_code", It.IsAny<string>(), It.IsAny<string>()))
                        .ReturnsAsync((OAuthTokenResponse?)null);

                    return mock.Object;
                });

                // S'assurer que la base de données est créée
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                db.Database.EnsureCreated();
            }).ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "VotreCleSecrete123456789012345678901234567890" }
            }))
            .UseEnvironment("Testing");
}

public class OAuthServiceStub : OAuthService
{
    public OAuthServiceStub() : base(null!, new ConfigurationManager().AddJsonFile("appsettings.json").Build(), null!)
    {
    }
}