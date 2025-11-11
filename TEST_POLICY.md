# Politique de tests - IrcChat

## Règles générales

Lors de l'implémentation de toute nouvelle fonctionnalité, créer systématiquement :
1. **Tests unitaires** pour la logique métier
2. **Tests d'intégration** pour les endpoints API
3. **Tests de composants** pour l'UI Blazor

## Couverture minimale requise

- **API (Backend)** : ≥ 80% de couverture
- **Client (Frontend)** : ≥ 70% de couverture
- **Logique critique** : 100% de couverture

## Structure des tests

### Tests API (`tests/IrcChat.Api.Tests/`)
```
Authorization/
  - *HandlerTests.cs        # Tests des Authorization Handlers
Integration/
  - *EndpointsTests.cs      # Tests d'intégration des endpoints
Services/
  - *ServiceTests.cs        # Tests unitaires des services
Hubs/
  - *HubTests.cs            # Tests SignalR
Helpers/
  - TestDataBuilder.cs      # Builders pour données de test
  - TestDbContextFactory.cs # Factory pour DbContext en mémoire
```

### Tests Client (`tests/IrcChat.Client.Tests/`)
```
Pages/
  - *Tests.cs              # Tests des pages Blazor
Components/
  - *Tests.cs              # Tests des composants
Services/
  - *ServiceTests.cs       # Tests des services client
Helpers/
  - BunitTestContext.cs    # Contexte de test Bunit
```

## Patterns à suivre

### 1. Tests d'endpoints API
```csharp
[Fact]
public async Task GetResource_WithValidId_ShouldReturnResource()
{
    // Arrange
    var resource = TestDataBuilder.CreateResource();
    await SeedDatabase(resource);
    
    // Act
    var response = await _client.GetAsync($"/api/resources/{resource.Id}");
    
    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<Resource>();
    Assert.NotNull(result);
    Assert.Equal(resource.Id, result.Id);
}
```

### 2. Tests de services
```csharp
[Fact]
public async Task ServiceMethod_WithValidInput_ShouldReturnExpectedResult()
{
    // Arrange
    var mockDependency = new Mock<IDependency>();
    mockDependency.Setup(x => x.Method()).ReturnsAsync(expectedValue);
    var service = new MyService(mockDependency.Object);
    
    // Act
    var result = await service.MethodUnderTest(input);
    
    // Assert
    Assert.Equal(expectedValue, result);
    mockDependency.Verify(x => x.Method(), Times.Once);
}
```

### 3. Tests de composants Blazor
```csharp
[Fact]
public void Component_WhenRendered_ShouldDisplayExpectedContent()
{
    // Arrange
    var cut = RenderComponent<MyComponent>(parameters => parameters
        .Add(p => p.Property, value));
    
    // Act & Assert
    Assert.Contains("expected content", cut.Markup);
    Assert.NotNull(cut.Find("button"));
}
```

## Cas de tests obligatoires

Pour chaque fonctionnalité, tester :

### Endpoints API
- ✅ **Happy path** : fonctionnement normal
- ✅ **Validation** : données invalides
- ✅ **Authorization** : accès non autorisé
- ✅ **Not Found** : ressource inexistante
- ✅ **Edge cases** : cas limites

### Services
- ✅ **Logique métier** : tous les chemins d'exécution
- ✅ **Gestion d'erreurs** : exceptions et erreurs
- ✅ **Dépendances** : interactions avec les dépendances
- ✅ **États** : changements d'état

### Composants UI
- ✅ **Rendu** : affichage correct
- ✅ **Interactions** : clics, saisies, événements
- ✅ **États** : états différents du composant
- ✅ **Props** : différentes valeurs de paramètres

## Nomenclature

### Noms de fichiers
- `{Fonctionnalité}EndpointsTests.cs` pour les endpoints
- `{Service}ServiceTests.cs` pour les services
- `{Composant}Tests.cs` pour les composants

### Noms de tests
Format : `MethodName_Scenario_ExpectedBehavior`

Exemples :
- `CreateChannel_WithValidData_ShouldCreateChannel`
- `SendMessage_WhenChannelMuted_ShouldBlockMessage`
- `Login_WithInvalidCredentials_ShouldReturnUnauthorized`

## Outils et frameworks

### Backend
- **xUnit** : Framework de test
- **Moq** : Mocking
- **TestContainers** : Containers pour tests d'intégration (optionnel)
- **InMemory Database** : Base de données en mémoire pour tests

### Frontend
- **bUnit** : Tests de composants Blazor
- **MockHttp** : Mock des requêtes HTTP

## Assertions xUnit

Utiliser les assertions natives xUnit :

```csharp
// Égalité
Assert.Equal(expected, actual);
Assert.NotEqual(expected, actual);

// Nullité
Assert.Null(value);
Assert.NotNull(value);

// Booléens
Assert.True(condition);
Assert.False(condition);

// Collections
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Single(collection);
Assert.Contains(expectedItem, collection);

// Strings
Assert.Contains("substring", fullString);
Assert.StartsWith("prefix", fullString);
Assert.EndsWith("suffix", fullString);

// Types
Assert.IsType<ExpectedType>(obj);
Assert.IsAssignableFrom<BaseType>(obj);

// Exceptions
var ex = Assert.Throws<ExceptionType>(() => Method());
Assert.Equal("expected", ex.Message);
```

## Commandes de test

```powershell
# Tous les tests
dotnet test

# Tests API uniquement
dotnet test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj

# Tests Client uniquement
dotnet test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj

# Avec couverture
dotnet test --collect:"XPlat Code Coverage"

# En mode watch
dotnet watch test --project tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj
```

## Checklist avant commit

- [ ] Tous les tests passent
- [ ] Nouveaux tests créés pour la fonctionnalité
- [ ] Couverture de code maintenue/améliorée
- [ ] Tests de régression passent
- [ ] Documentation des tests mise à jour si nécessaire

## Format de demande

Quand tu demandes une fonctionnalité, utilise ce format :

```
Implémente [fonctionnalité] avec tests complets

Contexte :
- [Description de la fonctionnalité]
- [Comportement attendu]

Critères d'acceptation :
- [ ] Fonctionnalité implémentée
- [ ] Tests unitaires créés
- [ ] Tests d'intégration créés (si API)
- [ ] Tests de composants créés (si UI)
- [ ] Tous les tests passent
- [ ] Couverture ≥ 80%
```

## Exemple concret

**Au lieu de :**
```
Ajoute une fonctionnalité pour bannir des utilisateurs
```

**Demande :**
```
Implémente la fonctionnalité de bannissement d'utilisateurs avec tests

Contexte :
- Les admins peuvent bannir des utilisateurs
- Les utilisateurs bannis ne peuvent plus se connecter
- Le bannissement peut être temporaire ou permanent

Critères d'acceptation :
- [ ] Endpoint POST /api/admin/ban créé
- [ ] Vérification des permissions admin
- [ ] Tests d'intégration de l'endpoint
- [ ] Tests unitaires de la logique
- [ ] Tests du composant UI d'administration
- [ ] Tests de vérification de bannissement à la connexion
```

## Tests automatiques par fonctionnalité

### Nouvel endpoint API
Créer automatiquement :
```
✅ Happy path (200 OK)
✅ Validation (400 Bad Request)
✅ Authorization (401/403)
✅ Not Found (404)
✅ Edge cases spécifiques
```

### Nouveau service
Créer automatiquement :
```
✅ Toutes les méthodes publiques
✅ Gestion des exceptions
✅ Interactions avec dépendances (Moq)
✅ Edge cases et valeurs limites
```

### Nouveau composant Blazor
Créer automatiquement :
```
✅ Rendu initial avec différentes props
✅ Interactions utilisateur (clics, saisies)
✅ États différents du composant
✅ Événements et callbacks
```

## Notes importantes

- Les tests sont **obligatoires** pour toute nouvelle fonctionnalité
- Un PR sans tests sera **refusé**
- La couverture de code ne doit **jamais diminuer**
- Les tests doivent être **maintenables** et **lisibles**
- Privilégier la **qualité** sur la quantité
- Utiliser les **assertions xUnit natives** au lieu de FluentAssertions