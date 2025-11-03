# Bonnes pratiques de test - IrcChat

## ⚠️ Règles critiques

### 1. Toujours créer un nouveau scope pour les vérifications

**❌ MAUVAIS** - Utilise le même contexte (cache EF Core)
```csharp
[Fact]
public async Task Test_BadExample()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Arrange & Act
    await _client.PostAsync("/api/endpoint", data);

    // Assert - ❌ PROBLÈME: Utilise le même contexte
    var result = await db.SomeTable.FindAsync(id);
    result.Should().BeNull(); // Peut échouer à cause du cache
}
```

**✅ BON** - Crée un nouveau scope et contexte
```csharp
[Fact]
public async Task Test_GoodExample()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Arrange & Act
    await _client.PostAsync("/api/endpoint", data);

    // Assert - ✅ Nouveau contexte, pas de cache
    using var verifyScope = _factory.Services.CreateScope();
    using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var result = await verifyContext.SomeTable.FindAsync(id);
    result.Should().BeNull(); // Fiable
}
```

### Pourquoi ?

Entity Framework Core maintient un **cache de premier niveau** (Identity Map) :
- Les entités chargées sont trackées dans le contexte
- `FindAsync()` retourne d'abord depuis le cache
- Les changements en BDD ne sont pas visibles dans le même contexte

**Solution** : Créer un nouveau scope + nouveau contexte pour les assertions.

## 📋 Pattern de test complet

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // ===== ARRANGE =====
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Créer les données de test
    var testData = new Entity { ... };
    db.Entities.Add(testData);
    await db.SaveChangesAsync();

    // Configurer l'authentification si nécessaire
    var token = GenerateToken(user);
    _client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);

    // ===== ACT =====
    var response = await _client.PostAsJsonAsync("/api/endpoint", request);

    // ===== ASSERT =====
    // 1. Vérifier le status HTTP
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    // 2. Vérifier la réponse
    var result = await response.Content.ReadFromJsonAsync<ResponseType>();
    result.Should().NotBeNull();
    result!.Property.Should().Be(expectedValue);

    // 3. Vérifier les changements en BDD (NOUVEAU SCOPE!)
    using var verifyScope = _factory.Services.CreateScope();
    using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
    
    var savedEntity = await verifyContext.Entities.FindAsync(testData.Id);
    savedEntity.Should().NotBeNull();
    savedEntity!.Property.Should().Be(expectedValue);
}
```

## 🎯 Checklist de test

### Tests d'endpoints API

- [ ] **Happy path** - Fonctionnement normal
- [ ] **Validation** - Données invalides → BadRequest
- [ ] **Authentication** - Sans token → Unauthorized
- [ ] **Authorization** - Utilisateur non autorisé → Forbidden
- [ ] **Not Found** - Ressource inexistante → NotFound
- [ ] **Duplicate** - Création de doublon → BadRequest
- [ ] **Edge cases** - Valeurs limites, cas spéciaux

### Tests de services

- [ ] **Logique métier** - Tous les chemins d'exécution
- [ ] **Exceptions** - Gestion des erreurs
- [ ] **Dépendances** - Mocks et interactions
- [ ] **États** - Changements d'état corrects

### Tests SignalR Hub

- [ ] **Connexion/Déconnexion** - OnConnected, OnDisconnected
- [ ] **Envoi de messages** - Broadcast, groupes
- [ ] **Gestion des groupes** - Join, Leave
- [ ] **Clients** - Caller, All, Group, Client

## 🔧 Patterns utiles

### 1. Génération de token JWT pour tests

```csharp
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
```

### 2. Builder pattern pour données de test

```csharp
public static class TestDataBuilder
{
    public static ReservedUsername CreateUser(
        string username = "testuser",
        bool isAdmin = false)
    {
        return new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = username,
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = Guid.NewGuid().ToString(),
            Email = $"{username}@test.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = isAdmin
        };
    }
}
```

### 3. Tests SignalR avec mocks

```csharp
private readonly Mock<IHubCallerClients> _clientsMock;
private readonly Mock<IClientProxy> _callerMock;
private readonly Mock<HubCallerContext> _contextMock;
private readonly Mock<IGroupManager> _groupManagerMock;

public MyHubTests()
{
    _clientsMock = new Mock<IHubCallerClients>();
    _callerMock = new Mock<IClientProxy>();
    _contextMock = new Mock<HubCallerContext>();
    _groupManagerMock = new Mock<IGroupManager>();

    _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
    _contextMock.Setup(c => c.ConnectionId).Returns("test-conn-id");

    _hub = new MyHub(db, logger)
    {
        Clients = _clientsMock.Object,
        Context = _contextMock.Object,
        Groups = _groupManagerMock.Object
    };
}
```

## 🚫 Pièges à éviter

### ❌ Ne pas oublier SaveChangesAsync

```csharp
// ❌ MAUVAIS
db.Entities.Add(entity);
// Oubli de SaveChangesAsync - l'entité n'est pas en BDD!

// ✅ BON
db.Entities.Add(entity);
await db.SaveChangesAsync();
```

### ❌ Ne pas réutiliser le même HttpClient avec auth

```csharp
// ❌ MAUVAIS - Le token reste pour tous les tests
_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// ✅ BON - Créer un nouveau client ou nettoyer
var client = _factory.CreateClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
```

### ❌ Ne pas ignorer les codes de statut

```csharp
// ❌ MAUVAIS
var result = await response.Content.ReadFromJsonAsync<Result>();

// ✅ BON
response.StatusCode.Should().Be(HttpStatusCode.OK);
var result = await response.Content.ReadFromJsonAsync<Result>();
```

## 📊 Structure des tests

```
tests/
├── IrcChat.Api.Tests/
│   ├── Integration/           # Tests d'intégration (endpoints)
│   │   ├── *EndpointsTests.cs
│   ├── Services/              # Tests unitaires (services)
│   │   ├── *ServiceTests.cs
│   ├── Hubs/                  # Tests SignalR
│   │   ├── *HubTests.cs
│   └── Helpers/               # Utilitaires de test
│       ├── TestDataBuilder.cs
│       ├── HttpClientExtensions.cs
│       └── TestDbContextFactory.cs
```

## 🎯 Objectifs de couverture

- **Endpoints API** : ≥ 85%
- **Services** : ≥ 80%
- **Hubs SignalR** : ≥ 75%
- **Extensions** : ≥ 70%
- **Global** : ≥ 80%

## 🔍 Vérifier la couverture localement

```powershell
# Lancer les tests avec couverture
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Générer un rapport HTML
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"CoverageReport" -reporttypes:Html

# Ouvrir le rapport
Start-Process "CoverageReport/index.html"
```

## 📚 Ressources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [SignalR Testing](https://learn.microsoft.com/en-us/aspnet/core/signalr/testing)

## ✅ Exemple complet

Voir les tests existants :
- `ChannelDeleteEndpointsTests.cs` - Pattern complet avec vérification BDD
- `AdminManagementEndpointsTests.cs` - Tests avec autorisation
- `ChatHubTests.cs` - Tests SignalR avec mocks
- `OAuthEndpointsTests.cs` - Tests d'authentification