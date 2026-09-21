param(
    [string]$Configuration = "Release",
    [string]$Version = ""
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repoRoot "artifacts\win-x64"
$stageDirectory = Join-Path $repoRoot "artifacts\stage"
$zipDirectory = Join-Path $repoRoot "artifacts\dist"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $csproj = Join-Path $repoRoot "src\FCB1010.App\FCB1010.App.csproj"
    $match = Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if ($match) { $Version = $match.Matches[0].Groups[1].Value }
    else { $Version = "0.0.0" }
}

Write-Host "Building FCB1010 Studio $Version ($Configuration, win-x64 single-file)"

dotnet test (Join-Path $repoRoot "FCB1010.slnx") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

if (Test-Path $outputDirectory) { Remove-Item $outputDirectory -Recurse -Force }
if (Test-Path $stageDirectory) { Remove-Item $stageDirectory -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outputDirectory, $stageDirectory, $zipDirectory | Out-Null

dotnet publish (Join-Path $repoRoot "src\FCB1010.App\FCB1010.App.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outputDirectory
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exeName = "FCB1010Studio.exe"
$exePath = Join-Path $outputDirectory $exeName
if (-not (Test-Path $exePath)) { throw "Expected executable not found: $exePath" }

Copy-Item $exePath (Join-Path $stageDirectory $exeName) -Force
$readme = @"
FCB1010 Studio $Version
=======================

Windows x64 self-contained executable — no .NET install required.

1. Extract this zip (or run the .exe directly).
2. Run FCB1010Studio.exe
3. Select MIDI IN / OUT, LINK, then READ from your FCB1010.

Source and docs: https://github.com/gayancc/FCB1010-Editor
"@
Set-Content -Path (Join-Path $stageDirectory "README.txt") -Value $readme -Encoding UTF8

$zipName = "FCB1010Studio-$Version-win-x64.zip"
$zipPath = Join-Path $zipDirectory $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $stageDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

# Also keep a versioned bare exe next to the zip for direct download.
$versionedExe = Join-Path $zipDirectory "FCB1010Studio-$Version-win-x64.exe"
Copy-Item $exePath $versionedExe -Force

Write-Host ""
Write-Host "Published executable : $exePath"
Write-Host "Release zip          : $zipPath"
Write-Host "Versioned exe        : $versionedExe"
Get-Item $exePath, $zipPath, $versionedExe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
