<#
@tool
Name=Deploy WatchList Release
Category=Deployment
Description=Deploys a specific version of the application to Windows Desktop or a connected Android device.
Order=21
Icon=Publish
RequiresConfirmation=false
Hidden=false
#>

<#
.SYNOPSIS
    Deploy-Release.ps1 - Deploys a compiled WatchList release to a target environment.

.DESCRIPTION
    Installs and launches a specific version of the WatchList app from the archived Releases/ folder.
    Supports local Windows desktop copies (with shortcut creation) and Android APK sideloading via ADB.

.PARAMETER Version
    The version string of the release to deploy (e.g. 1.0.0.1). Must exist in the Releases/ directory.

.PARAMETER Platform
    The target deployment platform. Options: Windows, Android.

.PARAMETER Destination
    The installation folder path for Windows deployments. Default: $env:LOCALAPPDATA\WatchList-App.

.PARAMETER Device
    The target ADB device serial number for Android deployment (e.g. "emulator-5554").
    Auto-detects the first connected device if omitted.

.PARAMETER Run
    If set, launches the application immediately after successful deployment.

.PARAMETER CreateShortcut
    If set, creates a Desktop shortcut pointing to the deployed executable (applicable to Windows only).

.EXAMPLE
    .\Deploy-Release.ps1 -Version 1.0.0.1 -Platform Windows -CreateShortcut -Run
    Deploys version 1.0.0.1 to Local AppData, creates a desktop shortcut, and runs it.

.EXAMPLE
    .\Deploy-Release.ps1 -Version 1.0.0.1 -Platform Android -Run
    Installs version 1.0.0.1 to the connected Android device and opens it.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Windows", "Android")]
    [string]$Platform,

    [string]$Destination = "$env:LOCALAPPDATA\WatchList-App",

    [string]$Device = "",

    [switch]$Run,

    [switch]$CreateShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Resolve solution root and releases directory
$solutionRoot = Split-Path $PSScriptRoot -Parent
$releasesRoot = Join-Path $solutionRoot "Releases"
$versionReleaseDir = Join-Path $releasesRoot $Version

# 1. Validate version release folder exists
if (-not (Test-Path $versionReleaseDir)) 
{
    Write-Error "Release version '$Version' does not exist in the Releases folder: $versionReleaseDir. Build it first using Build-Release.ps1"
}

# 2. Deploy to Windows
if ($Platform -eq "Windows") 
{
    $windowsSource = Join-Path $versionReleaseDir "Windows"
    if (-not (Test-Path $windowsSource) -or (Get-ChildItem $windowsSource).Count -eq 0) 
    {
        Write-Error "No Windows release artifacts found in $windowsSource"
    }

    Write-Host "Deploying WatchList version $Version to Windows..." -ForegroundColor Cyan
    Write-Host "Destination: $Destination" -ForegroundColor Gray

    if (-not (Test-Path $Destination)) 
    {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }

    Write-Host "Copying files..." -ForegroundColor Gray
    try 
    {
        Copy-Item -Path "$windowsSource\*" -Destination $Destination -Recurse -Force
    }
    catch 
    {
        Write-Warning "Could not copy some files. The application might be running. Please close it and try again."
        throw $_
    }

    # Create shortcut
    if ($CreateShortcut) 
    {
        Write-Host "Creating Desktop shortcut..." -ForegroundColor Gray
        try 
        {
            $wshShell = New-Object -ComObject WScript.Shell
            $desktopPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "WatchLists.lnk")
            $shortcut = $wshShell.CreateShortcut($desktopPath)
            $shortcut.TargetPath = Join-Path $Destination "WatchLists.exe"
            $shortcut.WorkingDirectory = $Destination
            $shortcut.Description = "WatchList Client App - Version $Version"
            $shortcut.Save()
            Write-Host "Desktop shortcut created successfully at: $desktopPath" -ForegroundColor Green
        }
        catch 
        {
            Write-Warning "Failed to create desktop shortcut: $_"
        }
    }

    # Run
    if ($Run) 
    {
        Write-Host "Launching WatchList..." -ForegroundColor Green
        $exePath = Join-Path $Destination "WatchLists.exe"
        Start-Process -FilePath $exePath -WorkingDirectory $Destination
    }
}

# 3. Deploy to Android
if ($Platform -eq "Android") 
{
    $apkPath = Join-Path $versionReleaseDir "Android\WatchLists.apk"
    if (-not (Test-Path $apkPath)) 
    {
        Write-Error "No Android APK found at $apkPath"
    }

    Write-Host "Deploying WatchList version $Version to Android..." -ForegroundColor Cyan

    # Verify ADB is on PATH
    if (-not (Get-Command adb -ErrorAction SilentlyContinue)) 
    {
        Write-Error "ADB (Android Debug Bridge) command not found on PATH. Please install Android SDK or add ADB to system PATH."
    }

    # Detect devices
    $devicesOutput = & adb devices
    $lines = $devicesOutput -split "`r?\n" | Where-Object { $_ -match "\tdevice$" }
    
    $targetDevice = ""
    if ($Device) 
    {
        # Verify requested device is connected
        $found = $lines | Where-Object { $_ -match "^$Device\t" }
        if (-not $found) 
        {
            Write-Error "Specified device '$Device' is not connected. Connected devices:`n$devicesOutput"
        }
        $targetDevice = $Device
    }
    else 
    {
        if ($lines.Count -eq 0) 
        {
            Write-Error "No connected Android devices or emulators found. Please connect a device or start an emulator."
        }
        elseif ($lines.Count -gt 1) 
        {
            $targetDevice = ($lines[0] -split "`t")[0]
            Write-Warning "Multiple devices detected. Using first device: $targetDevice. You can specify a device using the -Device parameter."
        }
        else 
        {
            $targetDevice = ($lines[0] -split "`t")[0]
            Write-Host "Auto-detected device: $targetDevice" -ForegroundColor Gray
        }
    }

    $adbArgs = @("-s", $targetDevice)

    Write-Host "Installing APK onto device $targetDevice..." -ForegroundColor Gray
    & adb @adbArgs install -r $apkPath

    if ($LASTEXITCODE -ne 0) 
    {
        Write-Error "Failed to install APK onto device."
    }
    Write-Host "APK installed successfully." -ForegroundColor Green

    # Run
    if ($Run) 
    {
        $packageName = "com.companyname.watchlists"
        Write-Host "Launching app $packageName on device..." -ForegroundColor Green
        & adb @adbArgs shell am force-stop $packageName | Out-Null
        Start-Sleep -Seconds 1
        & adb @adbArgs shell monkey -p $packageName -c android.intent.category.LAUNCHER 1 | Out-Null
    }
}
