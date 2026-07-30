# Build-Release.ps1
# Automates compiling, version bumping, and archiving build artifacts for WatchList.

param(
    [ValidateSet("Major", "Minor", "Patch", "Build")]
    [string]$Increment = "Build",

    [string]$Version,

    [ValidateSet("All", "Windows", "Android")]
    [string]$Platform = "All",

    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$versionPath = Join-Path $PSScriptRoot "version.txt"

# 1. Determine current version
if (-not (Test-Path $versionPath))
{
    Write-Host "version.txt not found. Initializing to 1.0.0.0" -ForegroundColor Yellow
    "1.0.0.0" | Out-File -FilePath $versionPath -Encoding utf8
}

$currentVersionStr = (Get-Content -Path $versionPath -Raw).Trim()
Write-Host "Current version: $currentVersionStr" -ForegroundColor Gray

$parts = $currentVersionStr -split '\.'
while ($parts.Count -lt 4)
{
    $parts += "0"
}

$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]
$build = [int]$parts[3]

# 2. Determine new version
if ($Version)
{
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$')
    {
        Write-Error "Invalid version format '$Version'. Must be major.minor.patch.build (e.g. 1.0.2.5)"
    }
    $newVersion = $Version
    $newParts = $Version -split '\.'
    $major = [int]$newParts[0]
    $minor = [int]$newParts[1]
    $patch = [int]$newParts[2]
    $build = [int]$newParts[3]
}
else
{
    switch ($Increment)
    {
        "Major" {
            $major++
            $minor = 0
            $patch = 0
            $build = 0
        }
        "Minor" {
            $minor++
            $patch = 0
            $build = 0
        }
        "Patch" {
            $patch++
            $build = 0
        }
        "Build" {
            $build++
        }
    }
    $newVersion = "$major.$minor.$patch.$build"
}

Write-Host "New target version: $newVersion" -ForegroundColor Green

# Calculate version code for Android (must be an integer, max 2100000000)
$versionCode = ($major * 1000000) + ($minor * 10000) + ($patch * 100) + $build

# 3. Restore NuGet packages first to handle multi-targeting cleanly
Write-Host "Restoring NuGet packages..." -ForegroundColor Gray
dotnet restore "$PSScriptRoot\WatchLists\WatchLists.csproj"

# 4. Optionally Clean
if ($Clean)
{
    Write-Host "Cleaning bin and obj folders..." -ForegroundColor Yellow
    Get-ChildItem -Path $PSScriptRoot -Include bin,obj -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}

$releasesRoot = Join-Path $PSScriptRoot "Releases"
$versionReleaseDir = Join-Path $releasesRoot $newVersion
$androidDest = Join-Path $versionReleaseDir "Android"
$windowsDest = Join-Path $versionReleaseDir "Windows"

# 5. Build Android
if ($Platform -eq "All" -or $Platform -eq "Android")
{
    Write-Host "`n==================================================" -ForegroundColor Cyan
    Write-Host " Building Android (version: $newVersion, code: $versionCode)" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    dotnet publish "$PSScriptRoot\WatchLists\WatchLists.csproj" -c Release -f net9.0-android --no-restore /p:ApplicationDisplayVersion="$newVersion" /p:ApplicationVersion=$versionCode

    # Locate APK
    $androidOutputDir = Join-Path $PSScriptRoot "WatchLists\bin\Release\net9.0-android"
    if (-not (Test-Path $androidOutputDir))
    {
        Write-Error "Android build output directory not found at $androidOutputDir"
    }

    $apkFiles = Get-ChildItem -Path $androidOutputDir -Filter "*.apk" -Recurse
    $selectedApk = $apkFiles | Where-Object { $_.Name -like "*-Signed.apk" } | Select-Object -First 1
    if (-not $selectedApk)
    {
        $selectedApk = $apkFiles | Where-Object { $_.Name -notlike "*unsigned*" } | Select-Object -First 1
    }
    if (-not $selectedApk)
    {
        $selectedApk = $apkFiles | Select-Object -First 1
    }

    if (-not $selectedApk)
    {
        Write-Error "Could not find any compiled APK file in $androidOutputDir"
    }

    Write-Host "Found APK: $($selectedApk.FullName)" -ForegroundColor Gray
    if (-not (Test-Path $androidDest))
    {
        New-Item -ItemType Directory -Path $androidDest -Force | Out-Null
    }

    $apkTarget = Join-Path $androidDest "WatchLists.apk"
    Copy-Item -Path $selectedApk.FullName -Destination $apkTarget -Force
    Write-Host "Saved Android artifact: $apkTarget" -ForegroundColor Green
}

# 6. Build Windows
if ($Platform -eq "All" -or $Platform -eq "Windows")
{
    Write-Host "`n==================================================" -ForegroundColor Cyan
    Write-Host " Building Windows Desktop (version: $newVersion)" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    dotnet publish "$PSScriptRoot\WatchLists\WatchLists.csproj" -c Release -f net9.0-windows10.0.19041.0 -r win10-x64 --self-contained false --no-restore /p:ApplicationDisplayVersion="$newVersion" /p:ApplicationVersion="$newVersion"

    $publishDir = "$PSScriptRoot\WatchLists\bin\Release\net9.0-windows10.0.19041.0\win10-x64\publish"
    if (-not (Test-Path $publishDir))
    {
        Write-Error "Windows publish output directory not found at $publishDir"
    }

    if (-not (Test-Path $windowsDest))
    {
        New-Item -ItemType Directory -Path $windowsDest -Force | Out-Null
    }

    # Clean old windows dest contents if any
    Remove-Item -Path "$windowsDest\*" -Recurse -Force -ErrorAction SilentlyContinue

    Copy-Item -Path "$publishDir\*" -Destination $windowsDest -Recurse -Force
    Write-Host "Saved Windows Desktop artifacts to: $windowsDest" -ForegroundColor Green
}

# 7. Update version.txt if build succeeded
$newVersion | Out-File -FilePath $versionPath -Encoding utf8
Write-Host "`nRelease $newVersion built and archived successfully!" -ForegroundColor Green
