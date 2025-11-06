# Directives de codage - IrcChat

## Conventions C# à respecter

### Accolades obligatoires
✅ **Toujours utiliser des accolades** même pour les blocs d'une seule ligne :
```csharp
// ✅ BON
if (condition)
{
    DoSomething();
}

// ❌ ÉVITER
if (condition)
    DoSomething();

// ✅ BON
for (int i = 0; i < 10; i++)
{
    Process(i);
}

// ❌ ÉVITER
for (int i = 0; i < 10; i++)
    Process(i);
```

### Logging
✅ **Toujours utiliser ILogger** au lieu de Console.WriteLine :
```csharp
// ✅ BON - Utilisation d'ILogger avec paramètres structurés
public class MyService(ILogger<MyService> logger)
{
    public async Task ProcessAsync(string userId)
    {
        try
        {
            logger.LogInformation("Traitement démarré pour l'utilisateur {UserId}", userId);
            await DoWorkAsync();
            logger.LogInformation("Traitement terminé avec succès");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du traitement pour l'utilisateur {UserId}", userId);
            throw;
        }
    }
}

// ❌ ÉVITER - Console.WriteLine
public async Task ProcessAsync(string userId)
{
    try
    {
        Console.WriteLine($"Traitement démarré pour {userId}");
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur: {ex.Message}");
    }
}

// ✅ BON - Différents niveaux de log
logger.LogTrace("Détails de bas niveau pour le debugging");
logger.LogDebug("Informations de debug, état interne");
logger.LogInformation("Événement normal, opération réussie");
logger.LogWarning("Situation anormale mais gérable");
logger.LogError(ex, "Erreur qui nécessite attention");
logger.LogCritical(ex, "Erreur critique, système en danger");

// ✅ BON - Paramètres structurés (pas d'interpolation de string)
logger.LogInformation("Utilisateur {Username} a rejoint le salon {Channel}", username, channel);

// ❌ ÉVITER - Interpolation de string dans les logs
logger.LogInformation($"Utilisateur {username} a rejoint le salon {channel}");

// ✅ BON - Injection dans Blazor
@inject ILogger<MyComponent> Logger

@code {
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erreur lors du chargement des données");
        }
    }
}
```

**Pourquoi utiliser ILogger au lieu de Console.WriteLine ?**
- ✅ Permet de filtrer les logs par niveau (Debug, Info, Warning, Error)
- ✅ Logs structurés pour faciliter la recherche et l'analyse
- ✅ Peut être redirigé vers différents outputs (fichiers, services cloud, etc.)
- ✅ Meilleure performance avec le logging structuré
- ✅ Contexte de l'application automatiquement inclus

### Constructeurs primaires (C# 12+)
✅ **Toujours utiliser les constructeurs primaires** pour les classes avec injection de dépendances :
```csharp
// ✅ BON
public class MyService(ILogger<MyService> logger, IConfiguration config)
{
    private readonly string _apiKey = config["ApiKey"];
    
    public void DoSomething()
    {
        logger.LogInformation("Doing something");
    }
}

// ❌ ÉVITER
public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IConfiguration _config;
    
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
}
```

### API Fluente et extension methods
✅ **Utiliser la notation fluente** pour la configuration et l'enregistrement de services :
```csharp
// ✅ BON - Fluent API
services
    .AddDatabaseServices(configuration)
    .AddApplicationServices(configuration)
    .AddJwtAuthentication(configuration);

app
    .ConfigurePipeline()
    .MapApiEndpoints();

// ❌ ÉVITER - Appels séparés
services.AddDatabaseServices(configuration);
services.AddApplicationServices(configuration);
services.AddJwtAuthentication(configuration);
```

### Entity Framework
✅ **Utiliser la configuration fluente** dans `OnModelCreating` :
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Message>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Channel);
        entity.HasIndex(e => e.Timestamp);
    });
}
```

### LINQ et requêtes
✅ **Préférer la syntaxe de méthode fluente** :
```csharp
// ✅ BON
var users = await db.ConnectedUsers
    .Where(u => u.Channel == channel)
    .OrderBy(u => u.Username)
    .Select(u => new User { ... })
    .ToListAsync();

// ❌ ÉVITER - Query syntax
var users = await (from u in db.ConnectedUsers
                   where u.Channel == channel
                   orderby u.Username
                   select new User { ... }).ToListAsync();
```

### Initialiseurs de collection
✅ **Utiliser les initialiseurs de collection** plutôt que Add() :
```csharp
// ✅ BON
var list = new List<string>
{
    "item1",
    "item2",
    "item3"
};

var dict = new Dictionary<string, int>
{
    ["key1"] = 1,
    ["key2"] = 2
};

// ❌ ÉVITER
var list = new List<string>();
list.Add("item1");
list.Add("item2");
list.Add("item3");

var dict = new Dictionary<string, int>();
dict.Add("key1", 1);
dict.Add("key2", 2);

// ✅ BON - Collection expressions (C# 12)
List<string> list = ["item1", "item2", "item3"];

// ✅ BON - Spread operator (C# 12)
var combined = [..list1, ..list2];
```

### Expression-bodied members
✅ **Utiliser les expression-bodied members** pour les méthodes simples :
```csharp
// ✅ BON - Expression body pour méthodes simples
public string GetFullName() => $"{FirstName} {LastName}";

public int Calculate(int x, int y) => x + y;

public bool IsValid() => Age >= 18 && !string.IsNullOrEmpty(Name);

// ✅ BON - Properties
public string FullName => $"{FirstName} {LastName}";

public bool IsAdult => Age >= 18;

// ✅ BON - Accessors
private string _name;
public string Name
{
    get => _name;
    set => _name = value?.Trim() ?? string.Empty;
}

// ❌ ÉVITER - Bloc complet pour une expression simple
public string GetFullName()
{
    return $"{FirstName} {LastName}";
}

public int Calculate(int x, int y)
{
    return x + y;
}

// ⚠️ ACCEPTABLE - Méthodes complexes avec bloc
public async Task<User> GetUserAsync(int id)
{
    var user = await _db.Users.FindAsync(id);
    if (user == null)
    {
        throw new NotFoundException();
    }
    return user;
}

// ⚠️ ACCEPTABLE - Constructeurs (toujours avec bloc)
public MyService(ILogger logger, IConfiguration config)
{
    _logger = logger;
    _config = config;
}
```

### Expression-bodied members avec lambdas
✅ **Utiliser les expression-bodied members** pour les méthodes simples et lambdas :
```csharp
// ✅ BON - Lambdas avec expression body
var names = users.Select(u => u.Name);
var adults = users.Where(u => u.Age >= 18);
var sorted = users.OrderBy(u => u.LastName);

// ✅ BON - Lambda action avec expression body
users.ForEach(u => Console.WriteLine(u.Name));
button.Click += (s, e) => Close();

// ❌ ÉVITER - Lambdas avec blocs inutiles
var names = users.Select(u => 
{
    return u.Name;
});

var adults = users.Where(u => 
{
    return u.Age >= 18;
});

// ⚠️ ACCEPTABLE - Lambdas multi-instructions avec bloc
var processed = users.Select(u => 
{
    var fullName = $"{u.FirstName} {u.LastName}";
    var age = DateTime.Now.Year - u.BirthYear;
    return new { fullName, age };
});
```

## Architecture

- **Clean separation** : API, Client, Shared projects
- **Extension methods** pour organiser la configuration
- **Background services** pour les tâches périodiques
- **SignalR** pour la communication temps réel
- **EF Core** avec PostgreSQL

## Patterns utilisés

- Repository pattern (via DbContext)
- Dependency Injection
- Options pattern pour la configuration
- Background services pour les tâches planifiées

## Technologies

- .NET 9.0
- Blazor WebAssembly
- SignalR
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- OAuth 2.0 (Google, Facebook, Microsoft)

## Tests obligatoires

### Règle TDD (Test-Driven Development)

**Pour chaque nouvelle fonctionnalité, créer les tests EN MÊME TEMPS que le code :**

1. **Tests unitaires** pour la logique métier
2. **Tests d'intégration** pour les endpoints API
3. **Tests de composants** pour l'UI

Voir `TEST_POLICY.md` pour les détails complets.

### Exemples de tests à créer automatiquement

#### Nouvel endpoint API
```
✅ Happy path
✅ Validation des données
✅ Gestion des erreurs
✅ Authorization
✅ Not Found / Bad Request
```

#### Nouveau service
```
✅ Toutes les méthodes publiques
✅ Gestion des exceptions
✅ Interactions avec dépendances
✅ Edge cases
```

#### Nouveau composant Blazor
```
✅ Rendu initial
✅ Interactions utilisateur
✅ États différents
✅ Props/Parameters
```

### Format de demande

Quand tu demandes une fonctionnalité, précise simplement :
```
"Implémente [fonctionnalité]"
```