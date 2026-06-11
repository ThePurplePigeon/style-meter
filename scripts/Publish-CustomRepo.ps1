[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repoRoot "StyleMeter.sln"
$releaseOutputDirectory = Join-Path $repoRoot "StyleMeter\bin\x64\Release"
$packagerDirectory = Join-Path $releaseOutputDirectory "StyleMeter"
$builtPackagePath = Join-Path $packagerDirectory "latest.zip"
$builtManifestPath = Join-Path $releaseOutputDirectory "StyleMeter.json"
$repoManifestPath = Join-Path $repoRoot "repo.json"
$distDirectory = Join-Path $repoRoot "dist"
$distPackagePath = Join-Path $distDirectory "StyleMeter.zip"
$corePackageFiles = @(
    "StyleMeter.deps.json",
    "StyleMeter.dll",
    "StyleMeter.json"
)

$ownerAndRepo = "ThePurplePigeon/style-meter"
$rawRoot = "https://raw.githubusercontent.com/$ownerAndRepo/master"
$downloadUrl = "$rawRoot/dist/StyleMeter.zip"
$iconUrl = "$rawRoot/images/icon.png"

function Invoke-DotNet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-FileExists
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -Path $Path -PathType Leaf))
    {
        throw "Missing file: $Path"
    }
}

Write-Host "==> Restore"
Invoke-DotNet -Arguments @("restore", $solutionPath)

Write-Host "==> Build (Release)"
Invoke-DotNet -Arguments @("build", $solutionPath, "-c", "Release", "-v", "minimal")

Write-Host "==> Test (Release)"
Invoke-DotNet -Arguments @("test", $solutionPath, "-c", "Release", "-v", "minimal", "--no-build")

Assert-FileExists -Path $builtManifestPath
Assert-FileExists -Path $builtPackagePath

foreach ($fileName in $corePackageFiles)
{
    Assert-FileExists -Path (Join-Path $releaseOutputDirectory $fileName)
}

Write-Host "==> Package dist/StyleMeter.zip"
New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null
Copy-Item -Path $builtPackagePath -Destination $distPackagePath -Force

$builtManifest = Get-Content -Path $builtManifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$builtManifest.AssemblyVersion))
{
    throw "AssemblyVersion was not found in $builtManifestPath"
}

Write-Host "==> Sync repo.json metadata"
$entry = [pscustomobject][ordered]@{
    Author = [string]$builtManifest.Author
    Name = [string]$builtManifest.Name
    InternalName = [string]$builtManifest.InternalName
    AssemblyVersion = [string]$builtManifest.AssemblyVersion
    Description = [string]$builtManifest.Description
    ApplicableVersion = [string]$builtManifest.ApplicableVersion
    RepoUrl = "https://github.com/$ownerAndRepo"
    DalamudApiLevel = [int]$builtManifest.DalamudApiLevel
    Punchline = [string]$builtManifest.Punchline
    IsHide = $false
    IsTestingExclusive = $false
    DownloadLinkInstall = $downloadUrl
    DownloadLinkUpdate = $downloadUrl
    LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
    IconUrl = $iconUrl
    ImageUrls = @($iconUrl)
}

$repoManifestJson = ConvertTo-Json -InputObject @($entry) -Depth 10
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($repoManifestPath, $repoManifestJson, $utf8NoBom)

Write-Host "Publish complete."
Write-Host "Package: $distPackagePath"
Write-Host "AssemblyVersion: $($entry.AssemblyVersion)"
Write-Host "LastUpdate: $($entry.LastUpdate)"
