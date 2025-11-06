# Contributing to IrcChat

Merci de votre intérêt pour contribuer à IrcChat ! 🎉

## 🤖 Particularité de ce projet

**Ce projet est unique** : le code est principalement généré par **Claude (Anthropic)**, une IA développée par Anthropic. Le mainteneur (humain) effectue du code review et demande des corrections/améliorations à Claude.

### Comment ça fonctionne ?

1. **Spécification** → L'humain décrit la fonctionnalité à Claude
2. **Génération** → Claude génère le code et les tests
3. **Review** → L'humain vérifie la conformité et la qualité
4. **Validation** → GitHub Actions et SonarCloud valident automatiquement
5. **Merge** → Après validation du Quality Gate

## 🤝 Comment contribuer ?

### Option 1 : Suggérer des fonctionnalités (Recommandé)

La meilleure façon de contribuer est de **créer une Feature Request** détaillée :

1. Aller dans [Issues](../../issues)
2. Cliquer sur **"New Issue"**
3. Choisir **"✨ Feature Request"**
4. Remplir le template avec :
   - Description claire du problème
   - Solution proposée
   - Critères d'acceptation
   - Spécifications techniques (si possible)
   - Exigences de test

Claude implémentera ensuite la fonctionnalité selon vos spécifications !

### Option 2 : Signaler des bugs

Si vous trouvez un bug :

1. Vérifier qu'il n'existe pas déjà dans les [Issues](../../issues)
2. Créer un **"🐛 Bug Report"**
3. Fournir :
   - Étapes de reproduction
   - Comportement attendu vs actuel
   - Logs et screenshots
   - Informations d'environnement

### Option 3 : Contribuer du code (Avancé)

Si vous souhaitez contribuer du code directement :

#### Prérequis

- ✅ Lire et comprendre les [Directives de codage](CODING_GUIDELINES.md)
- ✅ Lire la [Politique de tests](TEST_POLICY.md)
- ✅ Lire les [Bonnes pratiques de test](TESTING_BEST_PRACTICES.md)
- ✅ Fork du repository
- ✅ .NET 9 SDK installé
- ✅ PostgreSQL 16+ installé

#### Workflow de contribution

1. **Fork & Clone**
   ```bash
   git clone https://github.com/VOTRE_USERNAME/IrcChat.git
   cd IrcChat
   git checkout -b feature/ma-fonctionnalite
   ```

2. **Configuration**
   ```bash
   # Restaurer les packages
   dotnet restore
   
   # Configurer la base de données
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=ircchat_dev;Username=postgres;Password=postgres"
   
   # Appliquer les migrations
   cd src/IrcChat.Api
   dotnet ef database update
   ```

3. **Développement**
   
   ⚠️ **IMPORTANT** : Respecter les conventions C# :
   
   ```csharp
   // ✅ Constructeurs primaires
   public class MyService(ILogger<MyService> logger)
   {
       public void Method() => logger.LogInformation("Hello");
   }
   
   // ✅ Accolades obligatoires
   if (condition)
   {
       DoSomething();
   }
   
   // ✅ Expression-bodied members
   public string GetName() => $"{FirstName} {LastName}";
   
   // ✅ API fluente
   services
       .AddDatabaseServices(configuration)
       .AddApplicationServices(configuration);
   ```

4. **Tests obligatoires**
   
   Pour **chaque** fonctionnalité, créer :
   
   ```bash
   # Tests unitaires
   tests/IrcChat.Api.Tests/Services/MonServiceTests.cs
   
   # Tests d'intégration (si endpoint API)
   tests/IrcChat.Api.Tests/Integration/MonEndpointTests.cs
   
   # Tests de composants (si UI)
   tests/IrcChat.Client.Tests/Components/MonComposantTests.cs
   ```
   
   **Exigences** :
   - ✅ Couverture ≥ 80%
   - ✅ Tous les tests passent
   - ✅ Respect du pattern AAA (Arrange/Act/Assert)
   - ✅ Naming : `MethodName_Scenario_ExpectedBehavior`

5. **Vérifications locales**
   
   ```bash
   # Build
   dotnet build
   
   # Tests
   dotnet test
   
   # Couverture
   dotnet test --collect:"XPlat Code Coverage"
   
   # Format
   dotnet format
   ```

6. **Commit**
   
   Utiliser les [Conventional Commits](https://www.conventionalcommits.org/) :
   
   ```bash
   git commit -m "feat(api): add user ban endpoint"
   git commit -m "fix(client): correct message display bug"
   git commit -m "test(api): add user service tests"
   git commit -m "docs(readme): update installation guide"
   ```
   
   Types autorisés :
   - `feat`: Nouvelle fonctionnalité
   - `fix`: Correction de bug
   - `test`: Ajout/modification de tests
   - `docs`: Documentation
   - `style`: Formatage
   - `refactor`: Refactoring
   - `perf`: Amélioration de performance
   - `chore`: Tâches diverses

7. **Pull Request**
   
   ```bash
   git push origin feature/ma-fonctionnalite
   ```
   
   Puis créer une PR en remplissant le template :
   - ✅ Description claire
   - ✅ Type de changement coché
   - ✅ Checklist complétée
   - ✅ Tests ajoutés
   - ✅ Documentation mise à jour

## 📋 Checklist avant PR

### Code
- [ ] Suit les [directives de codage](CODING_GUIDELINES.md)
- [ ] Constructeurs primaires utilisés
- [ ] Accolades pour tous les blocs
- [ ] Expression-bodied members pour méthodes simples
- [ ] API fluente pour configuration
- [ ] Pas de code mort ou commenté
- [ ] `dotnet format` exécuté

### Tests
- [ ] Tests unitaires créés/mis à jour
- [ ] Tests d'intégration créés (si endpoint)
- [ ] Tests de composants créés (si UI)
- [ ] Tous les tests passent
- [ ] Couverture ≥ 80%
- [ ] Respect des [bonnes pratiques](TESTING_BEST_PRACTICES.md)

### Documentation
- [ ] Commentaires XML pour API publiques
- [ ] README mis à jour (si nécessaire)
- [ ] CHANGELOG mis à jour
- [ ] Exemples ajoutés (si applicable)

### Qualité
- [ ] Aucun warning de compilation
- [ ] SonarCloud Quality Gate passera
- [ ] Pas de vulnérabilités introduites
- [ ] Performance vérifiée

## 🚫 Ce qui ne sera PAS accepté

- ❌ Code sans tests
- ❌ Code ne respectant pas les guidelines
- ❌ PR cassant les tests existants
- ❌ Code avec warnings de compilation
- ❌ Code diminuant la couverture globale
- ❌ Code sans documentation
- ❌ Commits mal formatés

## 📊 Quality Gates

Votre PR doit passer :

### GitHub Actions
- ✅ Build réussi
- ✅ Tous les tests passent
- ✅ `dotnet format --verify-no-changes`
- ✅ Migrations valides

### SonarCloud
- ✅ Quality Gate : **Passed**
- ✅ Coverage : ≥ 80%
- ✅ Maintainability : **A**
- ✅ Reliability : **A**
- ✅ Security : **A**
- ✅ Duplications : ≤ 3%

## 🎯 Types de contributions

### 🐛 Bug Fixes
- Impact immédiat
- Tests de régression obligatoires
- Documentation de la correction

### ✨ Nouvelles fonctionnalités
- Discussion préalable recommandée
- Feature Request détaillée
- Tests complets
- Documentation complète

### 📝 Documentation
- Corrections de typos
- Amélioration de la clarté
- Ajout d'exemples
- Traductions

### 🧪 Tests
- Augmentation de la couverture
- Amélioration des tests existants
- Tests de régression

### 🎨 UI/UX
- Screenshots avant/après obligatoires
- Tests de composants
- Responsive design

### ⚡ Performance
- Benchmarks avant/après
- Justification de l'amélioration
- Tests de charge si applicable

## 🔍 Code Review

### Ce que nous vérifions

1. **Conformité** : Respect des guidelines
2. **Tests** : Couverture et qualité
3. **Sécurité** : Pas de vulnérabilités
4. **Performance** : Pas de régression
5. **Maintenabilité** : Code lisible et documenté

### Délais

- Bugs critiques : < 24h
- Bugs : < 3 jours
- Features : < 1 semaine
- Documentation : < 3 jours

## 🤔 Questions ?

- 📖 Consultez la [documentation](README.md)
- 💬 Ouvrez une [Discussion](../../discussions)
- ❓ Créez une [Question](../../issues/new/choose)
- 📧 Contactez les mainteneurs

## 🌟 Reconnaissance

Les contributeurs sont listés dans :
- Le fichier [CONTRIBUTORS.md](CONTRIBUTORS.md)
- La section "Contributors" de GitHub
- Les notes de release

## 📜 Code de conduite

En contribuant, vous acceptez de :

1. **Être respectueux** envers tous les participants
2. **Accepter les critiques constructives**
3. **Se concentrer sur ce qui est le mieux pour le projet**
4. **Montrer de l'empathie** envers les autres membres de la communauté

Nous ne tolérons aucun :
- Harcèlement
- Discrimination
- Comportement inapproprié
- Spam ou publicité

## 📚 Ressources

### Documentation du projet
- [README.md](README.md) - Vue d'ensemble
- [CODING_GUIDELINES.md](CODING_GUIDELINES.md) - Standards de code
- [TEST_POLICY.md](TEST_POLICY.md) - Politique de tests
- [TESTING_BEST_PRACTICES.md](TESTING_BEST_PRACTICES.md) - Bonnes pratiques
- [SONARCLOUD_SETUP.md](SONARCLOUD_SETUP.md) - Configuration SonarCloud

### Technologies
- [.NET 9 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)

### Outils
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [bUnit Documentation](https://bunit.dev/)
- [SonarCloud](https://sonarcloud.io/)

## 🎉 Merci !

Merci d'avoir pris le temps de lire ce guide et de vouloir contribuer à IrcChat !

Votre contribution, qu'elle soit grande ou petite, est précieuse et appréciée. 💙

---

> 🤖 **Rappel** : Ce projet utilise Claude pour générer le code. Votre rôle en tant que contributeur est de fournir des spécifications claires et de valider que le code généré répond aux besoins. C'est une nouvelle façon de collaborer où l'IA et les humains travaillent ensemble !