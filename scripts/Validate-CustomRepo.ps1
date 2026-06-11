[CmdletBinding()]
param(
    [string]$ExpectedOwnerAndRepo = "ThePurplePigeon/style-meter"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$releaseOutputDirectory = Join-Path $repoRoot "StyleMeter\bin\x64\Release"
$builtManifestPath = Join-Path $releaseOutputDirectory "StyleMeter.json"
$builtPackagePath = Join-Path $releaseOutputDirectory "StyleMeter\latest.zip"
$repoManifestPath = Join-Path $repoRoot "repo.json"
$distPackagePath = Join-Path $repoRoot "dist\StyleMeter.zip"
$corePackageFiles = @(
    "StyleMeter.deps.json",
    "StyleMeter.dll",
    "StyleMeter.json"
)

function Assert-EqualValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Actual
    )

    if ($Actual -ne $Expected)
    {
        throw "$Name mismatch. Expected '$Expected' but found '$Actual'."
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

Assert-FileExists -Path $builtManifestPath
Assert-FileExists -Path $builtPackagePath
Assert-FileExists -Path $repoManifestPath
Assert-FileExists -Path $distPackagePath

$builtManifest = Get-Content -Path $builtManifestPath -Raw | ConvertFrom-Json
$builtAssemblyVersion = [string]$builtManifest.AssemblyVersion
if ([string]::IsNullOrWhiteSpace($builtAssemblyVersion))
{
    throw "AssemblyVersion is missing from release manifest: $builtManifestPath"
}

$repoEntries = Get-Content -Path $repoManifestPath -Raw | ConvertFrom-Json
if ($repoEntries -is [System.Array])
{
    if ($repoEntries.Count -lt 1)
    {
        throw "repo.json must contain at least one manifest entry."
    }

    $repoEntry = $repoEntries[0]
}
else
{
    $repoEntry = $repoEntries
}
$rawRoot = "https://raw.githubusercontent.com/$ExpectedOwnerAndRepo/master"
$expectedDownloadUrl = "$rawRoot/dist/StyleMeter.zip"
$expectedIconUrl = "$rawRoot/images/icon.png"
$expectedRepoUrl = "https://github.com/$ExpectedOwnerAndRepo"

Assert-EqualValue -Name "RepoUrl" -Expected $expectedRepoUrl -Actual ([string]$repoEntry.RepoUrl)
Assert-EqualValue -Name "DownloadLinkInstall" -Expected $expectedDownloadUrl -Actual ([string]$repoEntry.DownloadLinkInstall)
Assert-EqualValue -Name "DownloadLinkUpdate" -Expected $expectedDownloadUrl -Actual ([string]$repoEntry.DownloadLinkUpdate)
Assert-EqualValue -Name "IconUrl" -Expected $expectedIconUrl -Actual ([string]$repoEntry.IconUrl)
Assert-EqualValue -Name "AssemblyVersion" -Expected $builtAssemblyVersion -Actual ([string]$repoEntry.AssemblyVersion)
Assert-EqualValue -Name "DalamudApiLevel" -Expected "15" -Actual ([string]$repoEntry.DalamudApiLevel)

$lastUpdate = 0L
if (-not [long]::TryParse([string]$repoEntry.LastUpdate, [ref]$lastUpdate) -or $lastUpdate -le 0)
{
    throw "LastUpdate must be a positive Unix timestamp. Found '$($repoEntry.LastUpdate)'."
}

$imageUrls = @($repoEntry.ImageUrls)
if ($imageUrls.Count -lt 1 -or [string]$imageUrls[0] -ne $expectedIconUrl)
{
    throw "ImageUrls must include the icon URL."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $distPackagePath))
try
{
    $entryNames = @($zip.Entries | ForEach-Object { $_.FullName })

    $nestedEntries = @($entryNames | Where-Object { $_ -match "[\\/]" })
    if ($nestedEntries.Count -gt 0)
    {
        throw "dist/StyleMeter.zip should place files at zip root. Nested entries found: $($nestedEntries -join ', ')"
    }

    foreach ($coreFile in $corePackageFiles)
    {
        if ($entryNames -notcontains $coreFile)
        {
            throw "dist/StyleMeter.zip is missing core file '$coreFile'."
        }
    }

    $manifestEntry = $zip.Entries | Where-Object { $_.FullName -eq "StyleMeter.json" } | Select-Object -First 1
    if ($null -eq $manifestEntry)
    {
        throw "dist/StyleMeter.zip does not contain StyleMeter.json."
    }

    $manifestStream = $manifestEntry.Open()
    $manifestReader = New-Object System.IO.StreamReader($manifestStream)
    try
    {
        $zipManifest = $manifestReader.ReadToEnd() | ConvertFrom-Json
    }
    finally
    {
        $manifestReader.Dispose()
        $manifestStream.Dispose()
    }

    Assert-EqualValue -Name "dist/StyleMeter.zip AssemblyVersion" -Expected $builtAssemblyVersion -Actual ([string]$zipManifest.AssemblyVersion)
}
finally
{
    $zip.Dispose()
}

Write-Host "Custom repo metadata validation passed."
Write-Host "AssemblyVersion: $builtAssemblyVersion"
Write-Host "Package: $distPackagePath"
