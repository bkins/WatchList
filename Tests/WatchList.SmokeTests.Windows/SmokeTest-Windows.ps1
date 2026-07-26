<#
.SYNOPSIS
    WatchList Windows Smoke Test Suite

.DESCRIPTION
    Exercises the main user flows of the WatchList MAUI Windows app using the built-in
    Windows UI Automation (UIA3) API. No external tools or servers required.

.PARAMETER ExePath
    Full path to WatchLists.exe.
    Auto-detected from standard build output directories if not supplied.

.PARAMETER MaxWaitSeconds
    How long to wait for the app to be ready. Default: 30.

.PARAMETER KeepAppOpen
    Leave the app running after all tests complete.

.EXAMPLE
    .\SmokeTest-Windows.ps1
    .\SmokeTest-Windows.ps1 -ExePath "C:\...\WatchLists.exe"
#>
param(
    [string] $ExePath        = "",
    [int]    $MaxWaitSeconds = 30,
    [switch] $KeepAppOpen,
    [switch] $ForceFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Load UI Automation Assemblies ──────────────────────────────────────────

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms   # for SendKeys fallback

# Short aliases for UIA types
$AE   = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]

# ─── Load Common Helpers ────────────────────────────────────────────────────
$helpersPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Tests\Test-Helpers.ps1"
if (Test-Path $helpersPath) {
    . $helpersPath
}

# ─── Counters ────────────────────────────────────────────────────────────────
$script:Passed        = 0
$script:Failed        = 0
$script:Inconclusive  = 0
$script:Proc          = $null
$script:Window        = $null

# ─── Exe resolution ──────────────────────────────────────────────────────────

function Resolve-ExePath {
    if ($ExePath -and (Test-Path $ExePath)) { return $ExePath }

    $root = "C:\Users\benho\source\repos\WatchList\WatchLists"
    $tfm  = "net9.0-windows10.0.19041.0"
    $candidates = @(
        "$root\bin\Debug\$tfm\win10-x64\WatchLists.exe"
        "$root\bin\Debug\$tfm\WatchLists.exe"
        "$root\bin\Release\$tfm\win10-x64\WatchLists.exe"
        "$root\bin\Release\$tfm\WatchLists.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) { return $found }
    throw "Could not find WatchLists.exe. Build with: dotnet build WatchLists/WatchLists.csproj -f net9.0-windows10.0.19041.0"
}

# ─── UIA helpers ─────────────────────────────────────────────────────────────

function Find-ById {
    <# Polls for an element by AutomationId. Returns $null on timeout. #>
    param($Root, [string] $AutomationId, [int] $TimeoutSeconds = 10)
    $cond     = New-Object System.Windows.Automation.PropertyCondition(
                    $AE::AutomationIdProperty, $AutomationId)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $el = $Root.FindFirst($Tree::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Find-ByName {
    <# Polls for an element by UIA Name property. Returns $null on timeout. #>
    param($Root, [string] $Name, [int] $TimeoutSeconds = 8)
    $cond     = New-Object System.Windows.Automation.PropertyCondition(
                    $AE::NameProperty, $Name)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $el = $Root.FindFirst($Tree::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Find-AllByClass {
    param($Root, [string] $ClassName)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
                $AE::ClassNameProperty, $ClassName)
    return @($Root.FindAll($Tree::Descendants, $cond))
}

function Invoke-Element {
    <# Clicks/invokes an element using InvokePattern, then SelectionItem as fallback. #>
    param($Element)
    if ($null -eq $Element) { return $false }
    try {
        $pat = $Element.GetCurrentPattern(
                   [System.Windows.Automation.InvokePattern]::Pattern)
        $pat.Invoke()
        return $true
    } catch { }
    try {
        $pat = $Element.GetCurrentPattern(
                   [System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pat.Select()
        return $true
    } catch { }
    return $false
}

function Set-ElementText {
    param($Element, [string] $Value)
    if ($null -eq $Element) { return $false }
    try {
        $pat = $Element.GetCurrentPattern(
                   [System.Windows.Automation.ValuePattern]::Pattern)
        $pat.SetValue($Value)
        return $true
    } catch { }

    try {
        $editCond = New-Object System.Windows.Automation.PropertyCondition(
            $AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
        $editChild = $Element.FindFirst($Tree::Descendants, $editCond)
        if ($editChild) {
            try {
                $pat = $editChild.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                $pat.SetValue($Value)
                return $true
            } catch { }
            $Element = $editChild
        }
    } catch { }

    try { $Element.SetFocus() } catch { }
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait("^a")
    [System.Windows.Forms.SendKeys]::SendWait("{BACKSPACE}")
    [System.Windows.Forms.SendKeys]::SendWait($Value)
    return $true
}

function Navigate-Back {
    param($Window)
    $backBtn = Find-ById $Window "NavigationViewBackButton" -TimeoutSeconds 2
    if (-not $backBtn) { $backBtn = Find-ByName $Window "Back" -TimeoutSeconds 2 }
    if (-not $backBtn) { $backBtn = Find-ByName $Window "Navigate up" -TimeoutSeconds 2 }
    if ($backBtn) {
        Invoke-Element $backBtn | Out-Null
    } else {
        [System.Windows.Forms.SendKeys]::SendWait("%{LEFT}")
    }
    Start-Sleep -Seconds 1
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
    } catch {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Error: $_" -ForegroundColor DarkRed
        $script:Failed++
        if (Get-Command Take-WindowsScreenshot -ErrorAction SilentlyContinue) {
            $pidToPass = if ($script:Proc) { $script:Proc.Id } else { 0 }
            Take-WindowsScreenshot -FileNamePrefix "WatchList_Win_Fail" -ProcessId $pidToPass
        }
    }
}

# ─── Main Script Execution ──────────────────────────────────────────────────

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " WatchList Windows Smoke Test Suite" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$targetExe = Resolve-ExePath
Write-Host "Target Executable: $targetExe" -ForegroundColor DarkGray

# Launch application process
Write-Host "Starting process..." -ForegroundColor Yellow
$script:Proc = Start-Process -FilePath $targetExe -PassThru
Start-Sleep -Seconds 3

# Attach Root Window Element via Process Handle
$deadline = (Get-Date).AddSeconds($MaxWaitSeconds)
while ((Get-Date) -lt $deadline) {
    $script:Proc.Refresh()
    if ($script:Proc.MainWindowHandle -ne [System.IntPtr]::Zero) {
        $script:Window = $AE::FromHandle($script:Proc.MainWindowHandle)
        if ($script:Window) { break }
    }
    Start-Sleep -Milliseconds 500
}

if ($null -eq $script:Window) {
    Write-Error "Failed to locate WatchList main window within $MaxWaitSeconds seconds."
}

Write-Host "`n--- Running Test Steps ---" -ForegroundColor Cyan

Run-Test "Step 01: Verify Main Page Ready" {
    $searchBar = Find-ById $script:Window "WatchListSearchBar" -TimeoutSeconds 15
    if (-not $searchBar) {
        $searchBar = Find-ByName $script:Window "Search..." -TimeoutSeconds 5
    }
    if ($null -eq $searchBar) {
        throw "Main Window loaded but SearchBar control not found."
    }
}

Run-Test "Step 02: Open Add Item Page" {
    $addBtn = Find-ById $script:Window "AddToolbarItem" -TimeoutSeconds 8
    if (-not $addBtn) { $addBtn = Find-ByName $script:Window "Add" -TimeoutSeconds 5 }
    if (-not $addBtn) { throw "Add toolbar item not found." }

    Invoke-Element $addBtn | Out-Null
    Start-Sleep -Seconds 1

    $titleEntry = Find-ById $script:Window "TitleEntry" -TimeoutSeconds 8
    if (-not $titleEntry) { $titleEntry = Find-ByName $script:Window "Title" -TimeoutSeconds 5 }
    if (-not $titleEntry) { throw "Title entry not found on EditWatchItemPage." }
}

Run-Test "Step 03: Save New Watch Item" {
    $titleEntry = Find-ById $script:Window "TitleEntry" -TimeoutSeconds 5
    if ($titleEntry) {
        Set-ElementText $titleEntry "Interstellar Win Test" | Out-Null
    }

    $saveBtn = Find-ById $script:Window "SaveButton" -TimeoutSeconds 5
    if (-not $saveBtn) { $saveBtn = Find-ByName $script:Window "Save" -TimeoutSeconds 5 }
    if (-not $saveBtn) { throw "Save button not found." }

    Invoke-Element $saveBtn | Out-Null
    Start-Sleep -Seconds 2
}

Run-Test "Step 04: Filter Watch List" {
    $searchBar = Find-ById $script:Window "WatchListSearchBar" -TimeoutSeconds 5
    if ($searchBar) {
        Set-ElementText $searchBar "Interstellar" | Out-Null
        Start-Sleep -Seconds 1
    }
}

Run-Test "Step 05: Open Settings Page" {
    $settingsBtn = Find-ById $script:Window "SettingsToolbarItem" -TimeoutSeconds 8
    if (-not $settingsBtn) { $settingsBtn = Find-ByName $script:Window "Settings" -TimeoutSeconds 5 }
    if (-not $settingsBtn) { throw "Settings toolbar item not found." }

    Invoke-Element $settingsBtn | Out-Null
    Start-Sleep -Seconds 1

    $manageBtn = Find-ById $script:Window "ManageStreamingServicesButton" -TimeoutSeconds 8
    if (-not $manageBtn) { $manageBtn = Find-ByName $script:Window "Manage Streaming Services" -TimeoutSeconds 5 }
    if (-not $manageBtn) { throw "Settings page failed to load." }

    Navigate-Back $script:Window
}

Run-Test "Step 06: Open Logs Page" {
    $logsBtn = Find-ById $script:Window "LogsToolbarItem" -TimeoutSeconds 8
    if (-not $logsBtn) { $logsBtn = Find-ByName $script:Window "Logs" -TimeoutSeconds 5 }
    if (-not $logsBtn) { throw "Logs toolbar item not found." }

    Invoke-Element $logsBtn | Out-Null
    Start-Sleep -Seconds 1

    $copyLogsBtn = Find-ById $script:Window "CopyLogsButton" -TimeoutSeconds 8
    if (-not $copyLogsBtn) { $copyLogsBtn = Find-ByName $script:Window "Copy Logs" -TimeoutSeconds 5 }
    if (-not $copyLogsBtn) { throw "Logs page failed to load." }

    Navigate-Back $script:Window
}

Run-Test "Step 07: Open API Test Page" {
    $apiBtn = Find-ById $script:Window "ApiTestToolbarItem" -TimeoutSeconds 8
    if (-not $apiBtn) { $apiBtn = Find-ByName $script:Window "API Test" -TimeoutSeconds 5 }
    if (-not $apiBtn) { throw "API Test toolbar item not found." }

    Invoke-Element $apiBtn | Out-Null
    Start-Sleep -Seconds 1

    $execBtn = Find-ById $script:Window "ExecuteApiCallButton" -TimeoutSeconds 8
    if (-not $execBtn) { $execBtn = Find-ByName $script:Window "Execute API Call" -TimeoutSeconds 5 }
    if (-not $execBtn) { throw "API Test page failed to load." }

    Navigate-Back $script:Window
}

if ($ForceFailure) {
    Run-Test "Step 99: Forced Test Failure" {
        throw "Forced diagnostic failure."
    }
}

# ─── Cleanup & Summary ------------------------------------------------------

if (-not $KeepAppOpen -and $script:Proc -and -not $script:Proc.HasExited) {
    Write-Host "`nStopping process..." -ForegroundColor DarkGray
    Stop-Process -Id $script:Proc.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host " Windows Smoke Test Results: $Passed Passed, $Failed Failed" -ForegroundColor ($Failed -eq 0 ? "Green" : "Red")
Write-Host "==================================================" -ForegroundColor Cyan

if ($Failed -gt 0) {
    exit 1
} else {
    exit 0
}
