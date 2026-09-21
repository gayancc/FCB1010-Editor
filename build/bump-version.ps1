param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",
    [string]$Csproj = "",
    [switch]$Write
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Csproj)) {
    $Csproj = Join-Path $repoRoot "src\FCB1010.App\FCB1010.App.csproj"
}

function Get-CsprojVersion([string]$path) {
    $match = Select-String -Path $path -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if (-not $match) { return $null }
    return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-LatestTagVersion {
    $tags = @(git tag -l "v*" --sort=-v:refname 2>$null)
    if (-not $tags -or $tags.Count -eq 0) { return $null }
    return ([string]$tags[0]).Trim().TrimStart("v")
}

function Parse-SemVer([string]$version) {
    if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)(?:[-+].*)?$') {
        throw "Not a semver X.Y.Z version: '$version'"
    }
    return [pscustomobject]@{
        Major = [int]$Matches[1]
        Minor = [int]$Matches[2]
        Patch = [int]$Matches[3]
    }
}

function Format-SemVer($parts) {
    return "{0}.{1}.{2}" -f $parts.Major, $parts.Minor, $parts.Patch
}

function Bump-SemVer($parts, [string]$kind) {
    switch ($kind) {
        "major" { $parts.Major++; $parts.Minor = 0; $parts.Patch = 0 }
        "minor" { $parts.Minor++; $parts.Patch = 0 }
        default { $parts.Patch++ }
    }
    return $parts
}

$fromTag = Get-LatestTagVersion
$fromCsproj = Get-CsprojVersion $Csproj
$current = if ($fromTag) { $fromTag } elseif ($fromCsproj) { $fromCsproj } else { "0.0.0" }
$next = Format-SemVer (Bump-SemVer (Parse-SemVer $current) $Bump)

Write-Host "Current version : $current$(if ($fromTag) { ' (latest tag)' } elseif ($fromCsproj) { ' (csproj)' } else { '' })"
Write-Host "Bump            : $Bump"
Write-Host "Next version    : $next"

if ($Write) {
    $text = Get-Content -Path $Csproj -Raw
    if ($text -notmatch '<Version>[^<]+</Version>') {
        throw "No <Version> element found in $Csproj"
    }
    $updated = [regex]::Replace($text, '<Version>[^<]+</Version>', "<Version>$next</Version>", 1)
    Set-Content -Path $Csproj -Value $updated -NoNewline -Encoding UTF8
    Write-Host "Updated $Csproj -> $next"
}

# Machine-readable lines for CI
Write-Output "current=$current"
Write-Output "version=$next"
Write-Output "tag=v$next"
