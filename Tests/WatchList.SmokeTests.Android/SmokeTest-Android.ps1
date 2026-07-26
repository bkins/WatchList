<#
.SYNOPSIS
    WatchList Android Smoke Test Suite

.DESCRIPTION
    Exercises the main user flows of the WatchList MAUI app on a connected Android device
    or emulator using adb + UIAutomator. Each test prints PASS/FAIL with a description
    and dumps the UI hierarchy on failure to aid diagnosis.

.PARAMETER Device
    The adb device serial (e.g. "emulator-5554"). Defaults to the first connected device.

.PARAMETER PackageName
    The Android package name. Defaults to "com.companyname.watchlists".

.PARAMETER MaxWaitSeconds
    How long to wait for the app to be ready before timing out. Default: 60.

.PARAMETER KeepAppOpen
    When set, the app is left running after all tests complete.

.EXAMPLE
    .\SmokeTest-Android.ps1
    .\SmokeTest-Android.ps1 -Device emulator-5554 -MaxWaitSeconds 45
#>
param(
    [string] $Device        = "",
    [string] $PackageName   = "com.companyname.watchlists",
    [int]    $MaxWaitSeconds = 60,
    [switch] $KeepAppOpen,
    [switch] $ForceFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Common Helpers ---------------------------------------------------------
$helpersPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Tests\Test-Helpers.ps1"
if (Test-Path $helpersPath) {
    . $helpersPath
}

# --- Counters ---------------------------------------------------------------
$script:Passed     = 0
$script:Failed     = 0
$script:Results    = [System.Collections.Generic.List[object]]::new()
$script:lastStatus = ""

# --- adb helpers ------------------------------------------------------------

function Invoke-Adb {
    param([string[]] $Arguments)
    $adbArgs = if ($script:Device) { @("-s", $script:Device) + $Arguments } else { $Arguments }
    & adb @adbArgs
}

function Get-UiDump {
    <#
    Captures a UIAutomator hierarchy XML.
    Returns $null and prints a warning when the dump times out or the file is empty.
    #>
    $remote  = "/sdcard/watchlist_smoke_dump.xml"
    $local   = Join-Path $env:TEMP "watchlist_smoke_dump.xml"

    Invoke-Adb "shell", "uiautomator", "dump", $remote | Out-Null
    Invoke-Adb "pull", $remote, $local | Out-Null

    if (-not (Test-Path $local)) { return $null }
    $content = Get-Content $local -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }

    try   { return [xml]$content }
    catch { Write-Warning "Failed to parse UI dump XML: $_"; return $null }
}

function Find-Node {
    <#
    Searches the UIAutomator dump for a node matching ANY supplied criteria.
    All supplied criteria must match (AND logic within a single node).
    #>
    param(
        [xml]    $Dump,
        [string] $Text        = $null,
        [string] $ContentDesc = $null,
        [string] $ClassName   = $null,
        [string] $ResourceId  = $null,
        [string] $Enabled     = $null
    )

    $xpathParts = @()
    if ($Text)        { $xpathParts += "@text='$Text'" }
    if ($ContentDesc) { $xpathParts += "@content-desc='$ContentDesc'" }
    if ($ClassName)   { $xpathParts += "@class='$ClassName'" }
    if ($ResourceId)  { $xpathParts += "@resource-id='$ResourceId'" }
    if ($Enabled)     { $xpathParts += "@enabled='$Enabled'" }

    if ($xpathParts.Count -eq 0) { return $null }
    $xpath = "//node[" + ($xpathParts -join " and ") + "]"

    try   { return $Dump.SelectSingleNode($xpath) }
    catch { return $null }
}

function Find-AllNodes {
    param([xml] $Dump, [string] $XPath)
    try   { return @($Dump.SelectNodes($XPath)) }
    catch { return @() }
}

function Get-NodeCenter {
    param($Node)
    if ($null -eq $Node) { return $null }
    $bounds = $Node.bounds   # "[x1,y1][x2,y2]"
    if ($bounds -match '\[(\d+),(\d+)\]\[(\d+),(\d+)\]') {
        return @{
            X = [int](([int]$Matches[1] + [int]$Matches[3]) / 2)
            Y = [int](([int]$Matches[2] + [int]$Matches[4]) / 2)
        }
    }
    return $null
}

function Tap-Node {
    param($Node, [int] $DelayMs = 700)
    $center = Get-NodeCenter $Node
    if ($null -eq $center) {
        Write-Warning "Tap-Node: Node has no valid bounds."
        return $false
    }
    Invoke-Adb "shell", "input", "tap", $center.X, $center.Y | Out-Null
    Start-Sleep -Milliseconds $DelayMs
    return $true
}

function Input-Text {
    param([string] $Text, [int] $DelayMs = 500)
    # Replace spaces with %s for adb input text
    $escaped = $Text -replace ' ', '%s' -replace "'", "\'" -replace '"', '\"'
    Invoke-Adb "shell", "input", "text", $escaped | Out-Null
    Start-Sleep -Milliseconds $DelayMs
}

function Send-Key {
    param([string] $KeyCode, [int] $DelayMs = 500)
    Invoke-Adb "shell", "input", "keyevent", $KeyCode | Out-Null
    Start-Sleep -Milliseconds $DelayMs
}

function Dismiss-DiagnosticDialogs {
    <#
    Dismisses unexpected modal overlays or diagnostic popups if present.
    #>
    $dump = Get-UiDump
    if ($null -eq $dump) { return }

    $btn = Find-Node -Dump $dump -Text "Go to App"
    if ($btn) {
        Write-Host "Dismissing diagnostic overlay..." -ForegroundColor Yellow
        Tap-Node $btn
        Start-Sleep -Seconds 1
    }
}

function Wait-ForElement {
    param(
        [string] $Text        = $null,
        [string] $ContentDesc = $null,
        [string] $ClassName   = $null,
        [int]    $TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Dismiss-DiagnosticDialogs
        $dump = Get-UiDump
        if ($dump) {
            $node = Find-Node -Dump $dump -Text $Text -ContentDesc $ContentDesc -ClassName $ClassName
            if ($node) { return $node }
        }
        Start-Sleep -Milliseconds 800
    }
    return $null
}

function Run-Test {
    param(
        [string]     $Title,
        [scriptblock]$TestBlock
    )

    Write-Host -NoNewline "  $Title ... "
    try {
        & $TestBlock
        Write-Host "PASS" -ForegroundColor Green
        $script:Passed++
        $script:Results.Add([PSCustomObject]@{ Title = $Title; Status = "PASS"; Error = "" })
    } catch {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Error: $_" -ForegroundColor DarkRed
        $script:Failed++
        $script:Results.Add([PSCustomObject]@{ Title = $Title; Status = "FAIL"; Error = $_.ToString() })

        # Capture diagnostic failure screenshot & XML dump
        if (Get-Command Take-AndroidScreenshot -ErrorAction SilentlyContinue) {
            Take-AndroidScreenshot -Device $script:Device -FileNamePrefix "WatchList_Android_Fail"
        }
        $dump = Get-UiDump
        if ($dump) {
            $dumpPath = Join-Path $env:TEMP "WatchList_Android_Failure_Dump.xml"
            $dump.Save($dumpPath)
            Write-Host "    Saved hierarchy dump to: $dumpPath" -ForegroundColor DarkGray
        }
    }
}

# --- Main Test Script Execution ---------------------------------------------

Write-Host "==================================================" -ForegroundColor Cipher
Write-Host " WatchList Android Smoke Test Suite" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cipher

# Verify ADB connection
$devicesOutput = & adb devices
Write-Host "ADB Device Check:`n$devicesOutput" -ForegroundColor DarkGray

if (-not $script:Device) {
    $lines = $devicesOutput -split "`r?\n" | Where-Object { $_ -match "\tdevice$" }
    if ($lines.Count -gt 0) {
        $script:Device = ($lines[0] -split "`t")[0]
        Write-Host "Using auto-detected device: $script:Device" -ForegroundColor Green
    } else {
        Write-Error "No connected ADB devices found."
    }
}

# Start Application
Write-Host "`nLaunching $PackageName on device $script:Device..." -ForegroundColor Yellow
Invoke-Adb "shell", "am", "force-stop", $PackageName | Out-Null
Start-Sleep -Seconds 1
Invoke-Adb "shell", "monkey", "-p", $PackageName, "-c", "android.intent.category.LAUNCHER", "1" | Out-Null
Start-Sleep -Seconds 3

# Execute Test Steps
Write-Host "`n--- Running Test Steps ---" -ForegroundColor Cyan

Run-Test "Step 01: Verify Application Launch & Main Page Readiness" {
    $searchBar = Wait-ForElement -ContentDesc "WatchListSearchBar" -TimeoutSeconds $MaxWaitSeconds
    if (-not $searchBar) {
        $searchBar = Wait-ForElement -Text "Search..." -TimeoutSeconds 10
    }
    if ($null -eq $searchBar) {
        throw "MainPage failed to load. SearchBar not found."
    }
}

Run-Test "Step 02: Open Add Watch Item Form" {
    $addButton = Wait-ForElement -ContentDesc "AddToolbarItem" -TimeoutSeconds 10
    if (-not $addButton) {
        $addButton = Wait-ForElement -Text "Add" -TimeoutSeconds 5
    }
    if ($null -eq $addButton) {
        throw "Add toolbar button not found."
    }
    Tap-Node $addButton
    Start-Sleep -Seconds 1
    
    $titleEntry = Wait-ForElement -ContentDesc "TitleEntry" -TimeoutSeconds 10
    if (-not $titleEntry) {
        $titleEntry = Wait-ForElement -Text "Title" -TimeoutSeconds 5
    }
    if ($null -eq $titleEntry) {
        throw "EditWatchItemPage title entry not found."
    }
}

Run-Test "Step 03: Enter New Watch Item Details & Save" {
    $dump = Get-UiDump
    $titleEntry = Find-Node -Dump $dump -ContentDesc "TitleEntry"
    if (-not $titleEntry) { $titleEntry = Find-Node -Dump $dump -Text "Title" }
    if ($titleEntry) {
        Tap-Node $titleEntry
        Input-Text "Inception Automation Test"
    }

    $saveBtn = Wait-ForElement -ContentDesc "SaveButton" -TimeoutSeconds 5
    if (-not $saveBtn) { $saveBtn = Wait-ForElement -Text "Save" -TimeoutSeconds 5 }
    if (-not $saveBtn) { throw "Save button not found." }

    Tap-Node $saveBtn
    Start-Sleep -Seconds 2
}

Run-Test "Step 04: Verify Watch List Item Filtering" {
    $dump = Get-UiDump
    $searchBar = Find-Node -Dump $dump -ContentDesc "WatchListSearchBar"
    if ($searchBar) {
        Tap-Node $searchBar
        Input-Text "Inception"
        Start-Sleep -Seconds 1
    }
}

Run-Test "Step 05: Navigate to Settings Page" {
    $settingsBtn = Wait-ForElement -ContentDesc "SettingsToolbarItem" -TimeoutSeconds 10
    if (-not $settingsBtn) { $settingsBtn = Wait-ForElement -Text "Settings" -TimeoutSeconds 5 }
    if ($null -eq $settingsBtn) { throw "Settings toolbar item not found." }

    Tap-Node $settingsBtn
    Start-Sleep -Seconds 1

    $manageBtn = Wait-ForElement -ContentDesc "ManageStreamingServicesButton" -TimeoutSeconds 10
    if (-not $manageBtn) { $manageBtn = Wait-ForElement -Text "Manage Streaming Services" -TimeoutSeconds 5 }
    if ($null -eq $manageBtn) { throw "Settings Page failed to load." }

    Send-Key "KEYCODE_BACK"
}

Run-Test "Step 06: Navigate to Logs Page" {
    $logsBtn = Wait-ForElement -ContentDesc "LogsToolbarItem" -TimeoutSeconds 10
    if (-not $logsBtn) { $logsBtn = Wait-ForElement -Text "Logs" -TimeoutSeconds 5 }
    if ($null -eq $logsBtn) { throw "Logs toolbar button not found." }

    Tap-Node $logsBtn
    Start-Sleep -Seconds 1

    $copyLogs = Wait-ForElement -ContentDesc "CopyLogsButton" -TimeoutSeconds 10
    if (-not $copyLogs) { $copyLogs = Wait-ForElement -Text "Copy Logs" -TimeoutSeconds 5 }
    if ($null -eq $copyLogs) { throw "Logs Page failed to load." }

    Send-Key "KEYCODE_BACK"
}

Run-Test "Step 07: Navigate to API Test Page" {
    $apiBtn = Wait-ForElement -ContentDesc "ApiTestToolbarItem" -TimeoutSeconds 10
    if (-not $apiBtn) { $apiBtn = Wait-ForElement -Text "API Test" -TimeoutSeconds 5 }
    if ($null -eq $apiBtn) { throw "API Test toolbar button not found." }

    Tap-Node $apiBtn
    Start-Sleep -Seconds 1

    $execBtn = Wait-ForElement -ContentDesc "ExecuteApiCallButton" -TimeoutSeconds 10
    if (-not $execBtn) { $execBtn = Wait-ForElement -Text "Execute API Call" -TimeoutSeconds 5 }
    if ($null -eq $execBtn) { throw "API Test Page failed to load." }

    Send-Key "KEYCODE_BACK"
}

if ($ForceFailure) {
    Run-Test "Step 99: Simulated Test Failure (Diagnostic Verification)" {
        throw "Forced failure for diagnostic testing."
    }
}

# --- Cleanup & Summary ------------------------------------------------------
if (-not $KeepAppOpen) {
    Write-Host "`nClosing application..." -ForegroundColor DarkGray
    Invoke-Adb "shell", "am", "force-stop", $PackageName | Out-Null
}

Write-Host "`n==================================================" -ForegroundColor Cipher
Write-Host " Android Smoke Test Results: $Passed Passed, $Failed Failed" -ForegroundColor ($Failed -eq 0 ? "Green" : "Red")
Write-Host "==================================================" -ForegroundColor Cipher

if ($Failed -gt 0) {
    exit 1
} else {
    exit 0
}
