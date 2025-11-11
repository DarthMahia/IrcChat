# Script de migration FluentAssertions vers xUnit Assert
# Usage: .\Migrate-FluentToXunit.ps1 -Path "C:\MonProjet\Tests"

param(
    [Parameter(Mandatory=$true)]
    [string]$Path,
    
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf
)

function Replace-FluentAssertions {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $originalContent = $content
    
    # Supprimer les usings FluentAssertions
    $content = $content -replace 'using FluentAssertions;?\r?\n', ''
    $content = $content -replace 'using FluentAssertions\.[^;]+;?\r?\n', ''
    
    # Should().HaveCountGreaterThanOrEqualTo() -> Assert.True(x.Count >= y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCountGreaterThanOrEqualTo\(([^)]+)\);', 'Assert.True($1.Count >= $2);'
    
    # Should().HaveCountGreaterThan() -> Assert.True(x.Count > y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCountGreaterThan\(([^)]+)\);', 'Assert.True($1.Count > $2);'
    
    # Should().HaveCountLessThanOrEqualTo() -> Assert.True(x.Count <= y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCountLessThanOrEqualTo\(([^)]+)\);', 'Assert.True($1.Count <= $2);'
    
    # Should().HaveCountLessThan() -> Assert.True(x.Count < y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCountLessThan\(([^)]+)\);', 'Assert.True($1.Count < $2);'
    
    # Should().NotBeNullOrEmpty() -> Assert.NotEmpty() (pour strings et collections)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBeNullOrEmpty\(\);', 'Assert.NotEmpty($1);'
    
    # Should().BeNullOrEmpty() -> Assert.Empty() ou Assert.True(string.IsNullOrEmpty())
    # Note: Assert.Empty() pour collections, string.IsNullOrEmpty pour strings avec null possible
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeNullOrEmpty\(\);', 'Assert.True(string.IsNullOrEmpty($1));'
    
    # Should().NotBeNullOrWhiteSpace() -> Assert.False(string.IsNullOrWhiteSpace(x))
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBeNullOrWhiteSpace\(\);', 'Assert.False(string.IsNullOrWhiteSpace($1));'
    
    # Should().BeNullOrWhiteSpace() -> Assert.True(string.IsNullOrWhiteSpace(x))
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeNullOrWhiteSpace\(\);', 'Assert.True(string.IsNullOrWhiteSpace($1));'
    
    # Should().Be() -> Assert.Equal() (avec support des propriétés, indexeurs, méthodes et null-forgiving)
    # Utilise une regex plus robuste pour capturer tout sauf .Should()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.Be\(([^)]+)\);', 'Assert.Equal($2, $1);'
    
    # Should().NotBe() -> Assert.NotEqual()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBe\(([^)]+)\);', 'Assert.NotEqual($2, $1);'
    
    # Should().BeTrue() -> Assert.True()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeTrue\(\);', 'Assert.True($1);'
    
    # Should().BeFalse() -> Assert.False()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeFalse\(\);', 'Assert.False($1);'
    
    # Should().BeNull() -> Assert.Null()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeNull\(\);', 'Assert.Null($1);'
    
    # Should().NotBeNull() -> Assert.NotNull()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBeNull\(\);', 'Assert.NotNull($1);'
    
    # Should().BeEmpty() -> Assert.Empty()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeEmpty\(\);', 'Assert.Empty($1);'
    
    # Should().NotBeEmpty() -> Assert.NotEmpty()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBeEmpty\(\);', 'Assert.NotEmpty($1);'
    
    # Should().Contain() avec lambda -> Assert.Contains(collection, predicate)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.Contain\((\([^)]+\)\s*=>\s*[^;]+)\);', 'Assert.Contains($1, $2);'
    
    # Should().Contain() simple -> Assert.Contains()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.Contain\(([^)]+)\);', 'Assert.Contains($2, $1);'
    
    # Should().NotContain() avec lambda -> Assert.DoesNotContain(collection, predicate)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotContain\((\([^)]+\)\s*=>\s*[^;]+)\);', 'Assert.DoesNotContain($1, $2);'
    
    # Should().NotContain() simple -> Assert.DoesNotContain()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotContain\(([^)]+)\);', 'Assert.DoesNotContain($2, $1);'
    
    # Should().BeGreaterThan() -> Assert.True(x > y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeGreaterThan\(([^)]+)\);', 'Assert.True($1 > $2);'
    
    # Should().BeLessThan() -> Assert.True(x < y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeLessThan\(([^)]+)\);', 'Assert.True($1 < $2);'
    
    # Should().BeGreaterOrEqualTo() -> Assert.True(x >= y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeGreaterOrEqualTo\(([^)]+)\);', 'Assert.True($1 >= $2);'
    
    # Should().BeLessOrEqualTo() -> Assert.True(x <= y)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeLessOrEqualTo\(([^)]+)\);', 'Assert.True($1 <= $2);'
    
    # Should().BeAfter() -> Assert.True(x > y) (pour dates)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeAfter\(([^)]+)\);', 'Assert.True($1 > $2);'
    
    # Should().BeBefore() -> Assert.True(x < y) (pour dates)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeBefore\(([^)]+)\);', 'Assert.True($1 < $2);'
    
    # Should().BeOnOrAfter() -> Assert.True(x >= y) (pour dates)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeOnOrAfter\(([^)]+)\);', 'Assert.True($1 >= $2);'
    
    # Should().BeOnOrBefore() -> Assert.True(x <= y) (pour dates)
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeOnOrBefore\(([^)]+)\);', 'Assert.True($1 <= $2);'
    
    # Should().BeOfType<T>() -> Assert.IsType<T>()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeOfType<([^>]+)>\(\);', 'Assert.IsType<$2>($1);'
    
    # Should().BeAssignableTo<T>() -> Assert.IsAssignableFrom<T>()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeAssignableTo<([^>]+)>\(\);', 'Assert.IsAssignableFrom<$2>($1);'
    
    # Should().HaveCount() -> Assert.Single() si count = 1, sinon Assert.Equal()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCount\(1\);', 'Assert.Single($1);'
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.HaveCount\(([^)]+)\);', 'Assert.Equal($2, $1.Count);'
    
    # Should().BeSameAs() -> Assert.Same()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.BeSameAs\(([^)]+)\);', 'Assert.Same($2, $1);'
    
    # Should().NotBeSameAs() -> Assert.NotSame()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.NotBeSameAs\(([^)]+)\);', 'Assert.NotSame($2, $1);'
    
    # Should().StartWith() -> Assert.StartsWith()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.StartWith\(([^)]+)\);', 'Assert.StartsWith($2, $1);'
    
    # Should().EndWith() -> Assert.EndsWith()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.EndWith\(([^)]+)\);', 'Assert.EndsWith($2, $1);'
    
    # Should().Match() avec lambda complexe
    # Note: pour les cas complexes comme Match(id => ...), il faut le faire manuellement
    # On va juste convertir les cas simples
    
    # Should().Match() simple -> Assert.Matches()
    $content = $content -replace '([^\s;]+?)\.Should\(\)\.Match\(([^(][^)]*)\);', 'Assert.Matches($2, $1);'
    
    # Should().Throw<T>() -> Assert.Throws<T>()
    $content = $content -replace '(\w+)\.Should\(\)\.Throw<([^>]+)>\(\);', 'Assert.Throws<$2>($1);'
    
    # Invoking().Should().Throw<T>() -> Assert.Throws<T>()
    $content = $content -replace '(\w+)\.Invoking\(([^)]+)\)\.Should\(\)\.Throw<([^>]+)>\(\);', 'Assert.Throws<$3>($2);'
    
    if ($content -ne $originalContent) {
        if ($WhatIf) {
            Write-Host "Serait modifié: $FilePath" -ForegroundColor Yellow
        } else {
            Set-Content $FilePath $content -NoNewline
            Write-Host "Modifié: $FilePath" -ForegroundColor Green
        }
        return $true
    }
    return $false
}

# Traiter tous les fichiers .cs
$files = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse
$modifiedCount = 0

Write-Host "Recherche de fichiers dans: $Path" -ForegroundColor Cyan
Write-Host "Fichiers trouvés: $($files.Count)" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "`nMode WhatIf activé - aucun fichier ne sera modifié`n" -ForegroundColor Yellow
}

foreach ($file in $files) {
    if (Replace-FluentAssertions -FilePath $file.FullName) {
        $modifiedCount++
    }
}

Write-Host "`nTerminé! $modifiedCount fichier(s) modifié(s)" -ForegroundColor Cyan

if ($modifiedCount -gt 0) {
    Write-Host "`nATTENTION: Vérifiez manuellement les conversions suivantes:" -ForegroundColor Yellow
    Write-Host "- Les assertions avec des expressions lambda complexes (Should().Match(x => ...))" -ForegroundColor Yellow
    Write-Host "- Les assertions chaînées (Should().Be().And...)" -ForegroundColor Yellow
    Write-Host "- Les assertions avec des messages personnalisés" -ForegroundColor Yellow
    Write-Host "- Les BeEquivalentTo() qui nécessitent une logique personnalisée" -ForegroundColor Yellow
    Write-Host "- Les Should().Contain() avec lambda doivent être vérifiées (ordre des paramètres)" -ForegroundColor Yellow
    Write-Host "`nREMARQUE: Les lignes suivantes de ExtensionsTests.cs doivent être converties manuellement:" -ForegroundColor Yellow
    Write-Host '  result.Should().Match(id => id == Environment.GetEnvironmentVariable("HOSTNAME") || id == Environment.MachineName);' -ForegroundColor Gray
    Write-Host "  -> Convertir en:" -ForegroundColor Yellow
    Write-Host '  Assert.True(result == Environment.GetEnvironmentVariable("HOSTNAME") || result == Environment.MachineName);' -ForegroundColor Green
}