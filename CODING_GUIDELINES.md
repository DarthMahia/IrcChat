# Directives de codage - IrcChat

## Conventions C# à respecter

### Usings et namespaces
✅ **Toujours utiliser les directives using** pour éviter les noms de types à rallonge :
```csharp
// ✅ BON - Using au début du fichier
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyApp.Services;

public class MyService
{
    [SuppressMessage("Rule", "RuleId", Justification = "Raison")]
    public void DoWork()
    {
        var options = new JsonSerializerOptions();
    }
}

// ❌ ÉVITER - Noms complets
namespace MyApp.Services;

public class MyService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Rule", "RuleId", Justification = "Raison")]
    public void DoWork()
    {
        var options = new System.Text.Json.JsonSerializerOptions();
    }
}

// ⚠️ EXCEPTION - Quand cela nuit à la lisibilité (conflit de noms)
using SystemTask = System.Threading.Tasks.Task;

public class MyTask
{
    // Ici on utilise le alias car "Task" pourrait être ambigu
    public SystemTask DoWorkAsync() => SystemTask.CompletedTask;
}
```

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

### Gestion des erreurs

⚠️ **CRITIQUE : Ne JAMAIS ignorer les exceptions silencieusement**

```csharp
// ❌ ÉVITER - Catch vide (erreur silencieuse)
try
{
    await DoSomethingAsync();
}
catch
{
    // Rien - L'erreur est ignorée !
}

// ❌ ÉVITER - Catch avec commentaire vague
try
{
    await DoSomethingAsync();
}
catch
{
    // Ignorer les erreurs
}

// ❌ ÉVITER - Catch qui log seulement en Debug
try
{
    await DoSomethingAsync();
}
catch (Exception ex)
{
    #if DEBUG
    logger.LogError(ex, "Erreur");
    #endif
}

// ✅ BON - Toujours logger les erreurs
try
{
    await DoSomethingAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Erreur lors de l'exécution de DoSomething");
    // Décider si on rethrow ou pas selon le contexte
}

// ✅ BON - Logger avec contexte
try
{
    await ProcessUserAsync(userId);
}
catch (Exception ex)
{
    logger.LogError(ex, "Erreur lors du traitement de l'utilisateur {UserId}", userId);
    throw; // Rethrow si nécessaire
}

// ✅ BON - Différencier les types d'exceptions
try
{
    await SaveDataAsync();
}
catch (DbUpdateException ex)
{
    logger.LogError(ex, "Erreur de base de données lors de la sauvegarde");
    throw new DataAccessException("Impossible de sauvegarder", ex);
}
catch (Exception ex)
{
    logger.LogError(ex, "Erreur inattendue lors de la sauvegarde");
    throw;
}

// ✅ ACCEPTABLE - Si vraiment besoin d'ignorer (avec justification)
try
{
    await module.DisposeAsync();
}
catch (Exception ex)
{
    // Justification: Le dispose ne doit jamais bloquer l'application
    // mais on log quand même l'erreur pour investigation
    logger.LogWarning(ex, "Erreur lors du dispose du module, ignorée");
}

// ✅ BON - Pour JavaScript Interop (cas particulier)
try
{
    await jsRuntime.InvokeVoidAsync("someFunction");
}
catch (JSException ex)
{
    logger.LogWarning(ex, "Erreur JS lors de l'appel à someFunction");
    // On peut choisir de ne pas rethrow pour JS errors
}
catch (Exception ex)
{
    logger.LogError(ex, "Erreur inattendue lors de l'interop JS");
    throw;
}
```

**Règles de gestion des erreurs :**

1. **Toujours logger** : Chaque catch doit avoir au minimum un log
2. **Contexte complet** : Logger avec les paramètres pertinents
3. **Niveau approprié** : 
   - `LogError` pour les vraies erreurs
   - `LogWarning` pour les erreurs qu'on peut ignorer
   - `LogDebug` pour les informations de débogage
4. **Rethrow si nécessaire** : Si l'erreur doit être gérée plus haut
5. **Justification** : Si vraiment besoin d'ignorer, ajouter un commentaire expliquant pourquoi

**Cas où on peut ignorer (avec log Warning) :**
- Dispose/Cleanup operations
- JavaScript Interop optionnel
- Opérations de cache (fallback possible)
- Optimisations non-critiques

**Cas où on ne doit JAMAIS ignorer :**
- Opérations de données (CRUD)
- Authentification/Autorisation
- Logique métier
- Initialisation critique

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

## Conventions CSS à respecter

### Contraste des couleurs (WCAG AA)

⚠️ **CRITIQUE : Toujours respecter les ratios de contraste WCAG pour l'accessibilité**

Les ratios minimum requis sont :
- **4.5:1** pour le texte normal (< 18pt ou < 14pt gras)
- **3:1** pour le texte large (≥ 18pt ou ≥ 14pt gras) et les composants UI

```css
/* ❌ MAUVAIS - Contraste insuffisant */
.button {
  background: #f56565;
  color: #fff; /* Ratio: 3.16:1 - INSUFFISANT pour texte normal */
}

.badge {
  background: #6e6e6e;
  color: #fff; /* Ratio: 4.36:1 - LIMITE, préférer mieux */
}

.text-muted {
  color: #888; /* Sur fond #1e1e1e - Ratio: 3.89:1 - INSUFFISANT */
}

/* ✅ BON - Contraste suffisant */
.button {
  background: #f56565;
  color: #000; /* Ratio: 10.35:1 - EXCELLENT */
}

.badge {
  background: #6e6e6e;
  color: #000; /* Ratio: 9.78:1 - EXCELLENT */
}

.text-muted {
  color: #aaa; /* Sur fond #1e1e1e - Ratio: 6.89:1 - BON */
}

/* ✅ BON - Nuances pour meilleur contraste sur fond sombre */
.text-primary {
  color: #9cc5ff; /* Au lieu de #8ab4f8 - Meilleur contraste */
}

.text-success {
  color: #68d391; /* Au lieu de #48bb78 - Meilleur contraste */
}

.text-danger {
  color: #fc8181; /* Au lieu de #f56565 - Meilleur contraste */
}

/* ✅ BON - Indicateurs d'état avec bon contraste */
.status-dot.online {
  background: #68d391; /* Au lieu de #48bb78 */
}

.status.connected {
  color: #68d391; /* Au lieu de #48bb78 */
}

.status.disconnected {
  color: #fc8181; /* Au lieu de #f56565 */
}
```

**Outil pour vérifier les contrastes :**
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Colour Contrast Analyser](https://www.tpgi.com/color-contrast-checker/)
- DevTools Chrome/Firefox (intégré dans l'inspecteur)

**Règles à suivre :**

1. **Toujours tester** les nouvelles couleurs avec un outil de vérification de contraste
2. **Privilégier** `color: #000` (noir) sur les fonds colorés clairs
3. **Utiliser** les variantes plus claires (#9cc5ff au lieu de #8ab4f8) pour le texte sur fond sombre
4. **Éviter** le blanc pur (#fff) sur les fonds colorés sauf si le ratio est > 4.5:1
5. **Documenter** les choix de couleurs dans les commentaires CSS si nécessaire

**Cas particuliers :**

```css
/* ✅ Gradients - S'assurer que le texte a un bon contraste sur TOUTES les parties */
.gradient-button {
  background: linear-gradient(135deg, #8ab4f8 0%, #48bb78 100%);
  color: #000; /* Noir pour garantir le contraste sur tout le gradient */
}

/* ✅ Badges avec indicateur - Priorité au contraste du texte principal */
.badge {
  background: #f56565;
  color: #000; /* Texte principal en noir */
}

.badge-icon {
  color: #fff; /* Icône peut être blanche si le badge est assez clair */
  font-weight: bold;
}

/* ✅ États hover - Maintenir le même niveau de contraste */
.button {
  background: #8ab4f8;
  color: #000;
}

.button:hover {
  background: #9cc5ff; /* Plus clair mais garde color: #000 */
  color: #000;
}
```

## Conventions JavaScript à respecter

### Utilisation de globalThis

⚠️ **TOUJOURS utiliser `globalThis` au lieu de `window`, `self` ou `global`**

```javascript
// ❌ MAUVAIS - Utilise window (spécifique au navigateur)
export function isMobileDevice() {
    const userAgent = navigator.userAgent || navigator.vendor || window.opera;
    
    if (window.innerWidth <= 768) {
        return true;
    }
    
    if ('ontouchstart' in window || navigator.maxTouchPoints > 0) {
        return true;
    }
    
    return false;
}

// ✅ BON - Utilise globalThis (portable, standard ES2020)
export function isMobileDevice() {
    const userAgent = navigator.userAgent || navigator.vendor || globalThis.opera;
    
    if (globalThis.innerWidth <= 768) {
        return true;
    }
    
    if ('ontouchstart' in globalThis || navigator.maxTouchPoints > 0) {
        return true;
    }
    
    return false;
}
```

**Pourquoi `globalThis` ?**

1. **Portabilité** : Fonctionne dans tous les environnements JavaScript
   - Navigateur : `globalThis === window`
   - Node.js : `globalThis === global`
   - Web Workers : `globalThis === self`
   - Service Workers : `globalThis === self`

2. **Standard ES2020** : Recommandation officielle ECMAScript

3. **Cohérence** : Un seul nom pour tous les environnements

4. **Future-proof** : Compatible avec les futurs environnements JavaScript

**Exemples d'utilisation :**

```javascript
// ✅ Accès aux propriétés globales
const width = globalThis.innerWidth;
const height = globalThis.innerHeight;
const location = globalThis.location;

// ✅ Vérification de disponibilité d'API
if ('serviceWorker' in globalThis.navigator) {
    // Service Worker disponible
}

if ('IntersectionObserver' in globalThis) {
    // Intersection Observer disponible
}

// ✅ Timers
globalThis.setTimeout(() => {
    console.log('Hello');
}, 1000);

// ✅ Événements
globalThis.addEventListener('resize', handleResize);

// ✅ Variables globales (à éviter mais si nécessaire)
globalThis.myGlobalVar = 'value';

// ✅ Dans des modules
export function getWindowInfo() {
    return {
        width: globalThis.innerWidth,
        height: globalThis.innerHeight,
        userAgent: globalThis.navigator.userAgent
    };
}
```

**Cas particuliers :**

```javascript
// ✅ Code qui doit être portable entre navigateur et Node.js
export function isNode() {
    return typeof globalThis.process !== 'undefined' 
        && globalThis.process.versions 
        && globalThis.process.versions.node;
}

export function isBrowser() {
    return typeof globalThis.window !== 'undefined' 
        && typeof globalThis.document !== 'undefined';
}

// ✅ Polyfill pour anciens navigateurs (si nécessaire)
if (typeof globalThis === 'undefined') {
    // Ne devrait jamais arriver avec les navigateurs modernes
    (function() {
        if (typeof self !== 'undefined') {
            self.globalThis = self;
        } else if (typeof window !== 'undefined') {
            window.globalThis = window;
        } else if (typeof global !== 'undefined') {
            global.globalThis = global;
        }
    })();
}
```

**À NE PAS faire :**

```javascript
// ❌ N'utilisez JAMAIS ces patterns obsolètes
var global = window || self || global;  // Obsolète
var root = typeof window !== 'undefined' ? window : global;  // Complexe et inutile

// ❌ N'accédez pas directement à window sauf si strictement nécessaire
window.location.href = '/page';  // Éviter

// ✅ Utilisez globalThis à la place
globalThis.location.href = '/page';
```

**Exceptions acceptables :**

Les seuls cas où `window` est acceptable :
- Détection spécifique du navigateur (vs Node.js)
- APIs strictement liées au DOM (déjà dans le contexte du navigateur)

```javascript
// ✅ Acceptable - Détection d'environnement
if (typeof window !== 'undefined') {
    // Code spécifique navigateur
    // Ici on peut utiliser window.document, etc.
}

// ✅ Acceptable - Dans un contexte DOM explicite
function attachToWindow(element) {
    // On sait qu'on est dans un navigateur
    window.addEventListener('scroll', () => {
        // ...
    });
}
```

## Architecture

- **Clean separation** : API, Client, Shared projects
- **Authorization Policies** : Gestion des autorisations avec le système ASP.NET Core
- **Endpoints** : Minimal API endpoints dans le dossier `Endpoints/`
- **Extension methods** pour organiser la configuration dans le dossier `Extensions/`
- **Background services** pour les tâches périodiques
- **SignalR** pour la communication temps réel
- **EF Core** avec PostgreSQL

## Patterns utilisés

- Repository pattern (via DbContext)
- Dependency Injection
- Options pattern pour la configuration
- Background services pour les tâches planifiées
- Minimal API endpoints pour les routes HTTP
- Authorization Policy pattern pour les autorisations

## Organisation des dossiers

```
src/IrcChat.Api/
├── Authorization/     # Authorization Policies (Requirement + Handler)
├── Data/              # DbContext et entités
├── Endpoints/         # Minimal API endpoints (routes HTTP)
├── Extensions/        # Extension methods (DI, configuration)
├── Hubs/              # SignalR hubs
├── Migrations/        # Migrations EF Core
├── Models/            # Modèles de domaine
└── Services/          # Services métier
```

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
