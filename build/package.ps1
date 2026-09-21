param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repoRoot "artifacts\win-x64"
dotnet test (Join-Path $repoRoot "FCB1010.slnx") -c $Configuration
dotnet publish (Join-Path $repoRoot "src\FCB1010.App\FCB1010.App.csproj") -c $Configuration -r win-x64 --self-contained true -o $outputDirectory
Write-Host "Published to $outputDirectory"
