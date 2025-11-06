#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Add a label to closed/merged PRs that have no "valid" changelog labels.

.DESCRIPTION
  Script run locally. Requires GITHUB_TOKEN env var with repo scope.
#>

param(
  [string]$Owner = "aguacongas"
  [string]$Repo = "IrcChat"
  [string]$From = '',
  [string]$To = 'HEAD',
  [string]$Since = '',
  [switch]$OnlyMerged,
  [string]$ValidLabels = 'feat,feature,fix,bug,perf,docs,chore,ci,refactor,style,test,build,deps,breaking',
  [string]$ExcludeLabel = 'changelog:exclude'
  [switch]$DryRun
)

if (-not $env:GITHUB_TOKEN) {
  Write-Error "Please set GITHUB_TOKEN environment variable with a token that has repo scope."
  exit 1
}

$headers = @{
  Authorization = "token $($env:GITHUB_TOKEN)"
  Accept = 'application/vnd.github+json'
  'User-Agent' = 'label-unlabeled-prs-script'
}

$normLabels = $ValidLabels -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

function Api-Get {
  param($Url)
  try {
    Invoke-RestMethod -Uri $Url -Headers $headers -Method Get
  } catch {
    throw "GET $Url failed: $($_.Exception.Message)"
  }
}

function Api-Post {
  param($Url, $Body)
  try {
    Invoke-RestMethod -Uri $Url -Headers $headers -Method Post -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 6)
  } catch {
    throw "POST $Url failed: $($_.Exception.Message)"
  }
}

function Ensure-LabelExists {
  param($label)
  $url = "https://api.github.com/repos/$Owner/$Repo/labels/$([uri]::EscapeDataString($label))"
  try {
    Api-Get $url | Out-Null
    Write-Verbose "Label '$label' exists."
  } catch {
    Write-Host "Label '$label' not found, creating..."
    if ($DryRun) { Write-Host "[dry-run] Would create label $label"; return }
    $createUrl = "https://api.github.com/repos/$Owner/$Repo/labels"
    $body = @{ name = $label; color = "ededed"; description = "PRs excluded from changelog generation (added by script)"} 
    Api-Post $createUrl $body | Out-Null
    Write-Host "Label '$label' created."
  }
}

function Get-PrNumbers-ByRange {
  param($base, $head)
  $prSet = [System.Collections.Generic.HashSet[int]]::new()
  if (-not $base) { return $prSet }
  $cmpUrl = "https://api.github.com/repos/$Owner/$Repo/compare/$([uri]::EscapeDataString($base))...$([uri]::EscapeDataString($head))"
  try {
    $cmp = Api-Get $cmpUrl
    $commits = $cmp.commits
  } catch {
    Write-Warning "compare failed; falling back to listing commits from head"
    $commits = @()
    $page = 1
    while ($true) {
      $cUrl = "https://api.github.com/repos/$Owner/$Repo/commits?sha=$([uri]::EscapeDataString($head))&per_page=100&page=$page"
      $resp = Api-Get $cUrl
      if (-not $resp) { break }
      $commits += $resp
      if ($resp.Count -lt 100) { break }
      $page++
    }
  }

  foreach ($c in $commits) {
    $sha = $c.sha
    try {
      $assocUrl = "https://api.github.com/repos/$Owner/$Repo/commits/$sha/pulls"
      $prs = Invoke-RestMethod -Uri $assocUrl -Headers $headers -Method Get -ErrorAction Stop
      foreach ($pr in $prs) { [void]$prSet.Add([int]$pr.number) }
    } catch {
      # ignore commit-associated lookup errors
    }
  }
  return $prSet
}

function Get-PrNumbers-BySince {
  param($sinceIso)
  $prSet = [System.Collections.Generic.HashSet[int]]::new()
  $page = 1
  while ($true) {
    $url = "https://api.github.com/repos/$Owner/$Repo/pulls?state=closed&per_page=100&page=$page"
    $resp = Api-Get $url
    if (-not $resp -or $resp.Count -eq 0) { break }
    foreach ($pr in $resp) {
      if ($sinceIso) {
        if (-not $pr.closed_at) { continue }
        if ([DateTime]::Parse($pr.closed_at) -lt [DateTime]::Parse($sinceIso)) { continue }
      }
      [void]$prSet.Add([int]$pr.number)
    }
    if ($resp.Count -lt 100) { break }
    $page++
  }
  return $prSet
}

# -- main
Write-Host "Owner: $Owner, Repo: $Repo"
Write-Host "Valid labels: $($validLabels -join ', ')"
Write-Host "Exclude label: $ExcludeLabel"
if ($From) { Write-Host "Range: $From .. $To" } elseif ($Since) { Write-Host "Since: $Since" } else { Write-Host "No range/since provided: scanning all closed PRs (may be slow)" }

Ensure-LabelExists -label $ExcludeLabel

$prNumbers = [System.Collections.Generic.HashSet[int]]::new()

if ($From) {
  $set = Get-PrNumbers-ByRange -base $From -head $To
  foreach ($n in $set) { [void]$prNumbers.Add($n) }
} elseif ($Since) {
  $set = Get-PrNumbers-BySince -sinceIso $Since
  foreach ($n in $set) { [void]$prNumbers.Add($n) }
} else {
  $set = Get-PrNumbers-BySince -sinceIso $null
  foreach ($n in $set) { [void]$prNumbers.Add($n) }
}

if ($prNumbers.Count -eq 0) {
  Write-Host "No PRs found for given filter."
  exit 0
}

$processed = 0
$labelled = 0

foreach ($prNumber in $prNumbers) {
  $processed++
  try {
    $pr = Api-Get "https://api.github.com/repos/$Owner/$Repo/pulls/$prNumber"
    if ($OnlyMerged -and -not $pr.merged_at) {
      Write-Verbose "#$prNumber not merged, skipping"
      continue
    }
    if ($pr.draft) {
      Write-Verbose "#$prNumber is draft, skipping"
      continue
    }
    # Collect labels on PR, normalised to lowercase
    $labels = @()
    foreach ($l in $pr.labels) { $labels += ($l.name).ToLowerInvariant().Trim() }

    # Check intersection with validLabels (case-insensitive)
    $hasValid = $false
    foreach ($vl in $normLabels) {
      if ($labels -contains $vl) {
        $hasValid = $true
        break
      }
    }

    if ($hasValid) {
      Write-Verbose "#$prNumber has valid labels: $($labels -join ', '); skipping"
      continue
    }
	
    if ($labels -contains $ExcludeLabel) {
      Write-Verbose "#$prNumber already has $ExcludeLabel; skipping"
      continue
    }
    
    if ($DryRun) {
      Write-Host "[dry-run] Would add label '$ExcludeLabel' to PR #$prNumber (labels: $($labels -join ', '))"
    } else {
      $addUrl = "https://api.github.com/repos/$Owner/$Repo/issues/$prNumber/labels"
      $body = @{ labels = @($ExcludeLabel) }
      Api-Post $addUrl $body | Out-Null
      Write-Host "Labeled PR #$prNumber with '$ExcludeLabel'"
      $labelled++
    }
  } catch {
    Write-Warning ("Error processing PR #{0}: {1}" -f $prNumber, $_.Exception.Message)
  }
}

Write-Host "Done. Processed: $processed; Labeled: $labelled."
