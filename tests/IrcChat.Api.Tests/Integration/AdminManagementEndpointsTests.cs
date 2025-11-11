using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class AdminManagementEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetAllUsers_AsAdmin_ShouldReturnUserList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-123",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regular_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "user-456",
            Email = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.AddRange(adminUser, regularUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin-management/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.NotNull(users);
        Assert.True(users.Count >= 2);
    }

    [Fact]
    public async Task GetAllUsers_AsNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regular_user2",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "user-789",
            Email = "user2@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(regularUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(regularUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin-management/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PromoteUser_AsAdmin_ShouldPromoteUser()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_promote",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-promote-123",
            Email = "admin_promote@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var targetUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "to_promote",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "promote-456",
            Email = "promote@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.AddRange(adminUser, targetUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/admin-management/{targetUser.Id}/promote",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedUser = await verifyContext.ReservedUsernames.FindAsync(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.IsAdmin);
    }

    [Fact]
    public async Task PromoteUser_AlreadyAdmin_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_test",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-test-123",
            Email = "admin_test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var alreadyAdmin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "already_admin",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "already-admin-456",
            Email = "already_admin@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        db.ReservedUsernames.AddRange(adminUser, alreadyAdmin);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/admin-management/{alreadyAdmin.Id}/promote",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DemoteUser_AsAdmin_ShouldDemoteUser()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser1 = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_demote",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-demote-123",
            Email = "admin_demote@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var adminUser2 = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "to_demote",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "demote-456",
            Email = "demote@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        db.ReservedUsernames.AddRange(adminUser1, adminUser2);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser1);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/admin-management/{adminUser2.Id}/demote",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var updatedUser = await verifyContext.ReservedUsernames.FindAsync(adminUser2.Id);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser!.IsAdmin);
    }

    [Fact]
    public async Task DemoteUser_Self_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin_self",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "admin-self-123",
            Email = "admin_self@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        db.ReservedUsernames.Add(adminUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync(
            $"/api/admin-management/{adminUser.Id}/demote",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAdminStatus_AsAdmin_ShouldReturnTrue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "check_admin",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "check-admin-123",
            Email = "check_admin@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        db.ReservedUsernames.Add(adminUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(adminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin-management/check-admin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdminStatusResponse>();
        Assert.NotNull(result);
        Assert.True(result!.IsAdmin);
    }

    [Fact]
    public async Task CheckAdminStatus_AsNonAdmin_ShouldReturnFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "check_regular",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "check-regular-123",
            Email = "check_regular@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(regularUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(regularUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin-management/check-admin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdminStatusResponse>();
        Assert.NotNull(result);
        Assert.False(result!.IsAdmin);
    }

    [SuppressMessage("Blocker Vulnerability", "S6781:JWT secret keys should not be disclosed", Justification = "This is a test")]
    private static string GenerateToken(ReservedUsername user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VotreCleSecrete123456789012345678901234567890"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("provider", user.Provider.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "IrcChatApi",
            audience: "IrcChatClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized fron json")]

    private sealed class UserResponse
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public ExternalAuthProvider Provider { get; init; }
        public bool IsAdmin { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime LastLoginAt { get; init; }
        public string? AvatarUrl { get; init; }
    }

    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized fron json")]
    private sealed class AdminStatusResponse
    {
        public bool IsAdmin { get; init; }
    }
}