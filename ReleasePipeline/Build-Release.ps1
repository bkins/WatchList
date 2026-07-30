<#
@tool
Name=Build WatchList Release
Category=Deployment
Description=Increments version, compiles the application, and archives build artifacts for targeted platforms.
Order=20
Icon=Build
RequiresConfirmation=false
Hidden=false
#>

<#
.SYNOPSIS
    Build-Release.ps1 - Bumps application version and packages deployment artifacts.

.DESCRIPTION
    Reads the current version from version.txt, increments the version based on the specified
    increment level (Major, Minor, Patch, Build) or uses an explicit version string, and compiles the
    MAUI project for target platforms (Android and/or Windows).
    
    If run with no arguments, or with the -Interactive switch, it launches a step-by-step guided wizard.
    The built files are archived in the Releases/ folder at the root of the solution.

.PARAMETER Increment
    The version component to increment. Options: Major, Minor, Patch, Build.
    - Major: Increments major number, sets minor/patch/build to 0. (e.g. 1.0.0.0 -> 2.0.0.0)
    - Minor: Increments minor number, sets patch/build to 0. (e.g. 1.0.0.0 -> 1.1.0.0)
    - Patch: Increments patch number, sets build to 0. (e.g. 1.0.0.0 -> 1.0.1.0)
    - Build: Increments build number. (e.g. 1.0.0.0 -> 1.0.0.1)

.PARAMETER Version
    An explicit version string in format Major.Minor.Patch.Build (e.g. 2.1.0.5).
    Takes precedence over the -Increment parameter.

.PARAMETER Platform
    The target build platform. Options: All, Windows, Android.

.PARAMETER Clean
    If set, deletes the bin/ and obj/ folders in the project directory before building.

.PARAMETER Interactive
    Launches the guided release build wizard.

.EXAMPLE
    .\Build-Release.ps1
    Launches the guided wizard to configure the build step-by-step.

.EXAMPLE
    .\Build-Release.ps1 -Increment Build
    Bumps the build segment (e.g. 1.0.0.1 -> 1.0.0.2) and builds all platforms.

.EXAMPLE
    .\Build-Release.ps1 -Increment Patch -Platform Windows -Clean
    Bumps patch segment, cleans, and builds Windows only.
#>

param(
    [ValidateSet("Major", "Minor", "Patch", "Build")]
    [string]$Increment,

    [string]$Version,

    [ValidateSet("All", "Windows", "Android")]
    [string]$Platform,

    [switch]$Clean,

    [switch]$Interactive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Resolve directories
$solutionRoot = Split-Path $PSScriptRoot -Parent
$versionPath = Join-Path $PSScriptRoot "version.txt"
$releasesRoot = Join-Path $solutionRoot "Releases"

# Determine if we should run in interactive mode
$isInteractive = $Interactive -or ($null -eq $Increment -and -not $Version -and -not $Platform)

# 1. Interactive wizard mode
if ($isInteractive) 
{
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host " WatchList Build Release Interactive Wizard" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    # Select Version Bump Level
    Write-Host "`n[1] Select Version Bump Level:" -ForegroundColor Gray
    Write-Host "  1) Build (increment 4th digit, e.g. 1.0.0.1 -> 1.0.0.2)"
    Write-Host "  2) Patch (increment 3rd digit, reset 4th)"
    Write-Host "  3) Minor (increment 2nd digit, reset 3rd/4th)"
    Write-Host "  4) Major (increment 1st digit, reset 2nd/3rd/4th)"
    Write-Host "  5) Explicit Version String (e.g. 2.1.0.0)"
    
    $vChoice = ""
    while ($vChoice -notmatch '^[1-5]$') 
    {
        $vChoice = Read-Host "Select option [1-5]"
    }

    if ($vChoice -eq "5") 
    {
        $Version = ""
        while ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') 
        {
            $Version = Read-Host "Enter explicit version (major.minor.patch.build)"
        }
    } 
    else 
    {
        $map = @{ "1"="Build"; "2"="Patch"; "3"="Minor"; "4"="Major" }
        $Increment = $map[$vChoice]
    }

    # Select Target Platform
    Write-Host "`n[2] Select Target Platform:" -ForegroundColor Gray
    Write-Host "  1) All Platforms (Windows + Android)"
    Write-Host "  2) Windows Desktop Only"
    Write-Host "  3) Android Only"
    
    $pChoice = ""
    while ($pChoice -notmatch '^[1-3]$') 
    {
        $pChoice = Read-Host "Select platform [1-3]"
    }
    $mapPlatform = @{ "1"="All"; "2"="Windows"; "3"="Android" }
    $Platform = $mapPlatform[$pChoice]

    # Clean option
    Write-Host "`n[3] Clean Build Folders?" -ForegroundColor Gray
    $cChoice = Read-Host "Clean bin/obj directories first? (y/n)"
    $Clean = $cChoice -match '^[yY]([eE][sS])?$'
} 
else 
{
    # Set default values if running from non-interactive CLI and omitted
    if (-not $Increment -and -not $Version) 
    {
        $Increment = "Build"
    }
    if (-not $Platform) 
    {
        $Platform = "All"
    }
}

# 2. Determine current version
if (-not (Test-Path $versionPath)) 
{
    Write-Host "version.txt not found. Initializing to 1.0.0.0" -ForegroundColor Yellow
    "1.0.0.0" | Out-File -FilePath $versionPath -Encoding utf8
}

$currentVersionStr = (Get-Content -Path $versionPath -Raw).Trim()
Write-Host "`nCurrent version: $currentVersionStr" -ForegroundColor Gray

$parts = $currentVersionStr -split '\.'
while ($parts.Count -lt 4) 
{
    $parts += "0"
}

$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]
$build = [int]$parts[3]

# Determine new version
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
dotnet restore "$solutionRoot\WatchLists\WatchLists.csproj"

# 4. Optionally Clean
if ($Clean) 
{
    Write-Host "Cleaning bin and obj folders..." -ForegroundColor Yellow
    Get-ChildItem -Path $solutionRoot -Include bin,obj -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}

$versionReleaseDir = Join-Path $releasesRoot $newVersion
$androidDest = Join-Path $versionReleaseDir "Android"
$windowsDest = Join-Path $versionReleaseDir "Windows"

# 5. Build Android
if ($Platform -eq "All" -or $Platform -eq "Android") 
{
    Write-Host "`n==================================================" -ForegroundColor Cyan
    Write-Host " Building Android (version: $newVersion, code: $versionCode)" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    dotnet publish "$solutionRoot\WatchLists\WatchLists.csproj" -c Release -f net9.0-android --no-restore /p:ApplicationDisplayVersion="$newVersion" /p:ApplicationVersion=$versionCode

    # Locate APK
    $androidOutputDir = Join-Path $solutionRoot "WatchLists\bin\Release\net9.0-android"
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

    dotnet publish "$solutionRoot\WatchLists\WatchLists.csproj" -c Release -f net9.0-windows10.0.19041.0 -r win10-x64 --self-contained false --no-restore /p:ApplicationDisplayVersion="$newVersion" /p:ApplicationVersion="$newVersion"

    $publishDir = "$solutionRoot\WatchLists\bin\Release\net9.0-windows10.0.19041.0\win10-x64\publish"
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
