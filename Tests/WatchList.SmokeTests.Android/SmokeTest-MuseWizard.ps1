<#
.SYNOPSIS
    Verifies wizard navigation and active-search cancellation using a disposable emulator package.
.DESCRIPTION
    Requires the ENH-5 test APK and the synthetic 201-row document in Android Downloads.
    Does not import data, reset databases, or operate the regular or phone acceptance packages.
#>
param(
    [string] $Device       = 'emulator-5554'
  , [string] $DocumentName = 'watchlist-enh5-201-synthetic.md'
  , [string] $EvidencePath = "$env:TEMP\WatchList-MuseWizard-Smoke"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!$Device.StartsWith('emulator-', [StringComparison]::Ordinal)) {
    throw 'This test is restricted to a disposable Android emulator.'
}

$packageName = 'com.companyname.watchlists.enh5test'
$adbPath = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
$script:checkpoint = 0
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null

function Invoke-TestAdb {
    param([string[]] $Arguments)
    $output = & $adbPath -s $Device @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb failed: $($Arguments -join ' ')" }
    return $output
}

function Read-WizardUi {
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        $dumpResult = (Invoke-TestAdb @('shell', 'uiautomator', 'dump', '/sdcard/muse-wizard-smoke.xml')) -join ''
        if ($dumpResult -notmatch 'ERROR') {
            $xmlText = (Invoke-TestAdb @('shell', 'cat', '/sdcard/muse-wizard-smoke.xml')) -join ''
            [xml] $document = $xmlText
            if ($document.SelectSingleNode('//node')) {
                $script:checkpoint++
                $xmlText | Set-Content -LiteralPath (Join-Path $EvidencePath "$script:checkpoint.xml") -Encoding utf8
                return $document
            }
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'The Android UI hierarchy did not become available.'
}

function Find-WizardText {
    param([xml] $Document, [string] $Text)
    return $Document.SelectNodes('//node') | Where-Object { $_.text -eq $Text } | Select-Object -First 1
}

function Tap-WizardText {
    param([xml] $Document, [string] $Text)
    $node = Find-WizardText $Document $Text
    if (!$node -or $node.enabled -ne 'true') { throw "Missing or disabled action: $Text" }
    if ($node.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') { throw "Invalid bounds: $Text" }
    $left, $top, $right, $bottom = $Matches[1..4] | ForEach-Object { [int] $_ }
    if ($right -le $left -or $bottom -le $top) { throw "Action is off screen: $Text" }
    $null = Invoke-TestAdb @('shell', 'input', 'tap', [string][int](($left + $right) / 2), [string][int](($top + $bottom) / 2))
}

function Assert-WizardContains {
    param([xml] $Document, [string] $Text)
    if (!(Find-WizardText $Document $Text)) { throw "Expected text: $Text" }
}

function Get-SearchStatus {
    param([xml] $Document)
    $node = $Document.SelectNodes('//node') | Where-Object { $_.text -match '^(Searching|Paused|Finished).*searched' } | Select-Object -First 1
    if (!$node) { throw 'Search-progress strip is missing.' }
    return [string] $node.text
}

$null = Invoke-TestAdb @('shell', 'am', 'force-stop', $packageName)
$resolved = Invoke-TestAdb @('shell', 'cmd', 'package', 'resolve-activity', '--brief', $packageName)
$activity = $resolved | Where-Object { $_.StartsWith("$packageName/") } | Select-Object -Last 1
if (!$activity) { throw 'Test package activity is unavailable.' }
$null = Invoke-TestAdb @('shell', 'am', 'start', '-W', '-n', $activity)
$focus = (Invoke-TestAdb @('shell', 'dumpsys', 'window')) -join "`n"
if ($focus -notmatch "mCurrentFocus=.*$([regex]::Escape($packageName))/") { throw 'The isolated test app did not become foreground.' }

$ui = Read-WizardUi
Tap-WizardText $ui 'Discover'
$ui = Read-WizardUi
Tap-WizardText $ui 'Muse preview'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Choose Markdown file'
Tap-WizardText $ui 'Choose Markdown file'
$ui = Read-WizardUi
Tap-WizardText $ui $DocumentName
$ui = Read-WizardUi
Assert-WizardContains $ui '201 document entries · 201 review items · 0 repeated entries grouped · 0 warnings'
Tap-WizardText $ui 'Start review'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Recommendation 1 of 201'
if ((Get-SearchStatus $ui) -notmatch '^Searching') { throw 'Searches were not actively pending; this does not prove cancellation.' }
$beforeStop = Get-SearchStatus $ui
Tap-WizardText $ui 'Approve & next'
# The search strip has fixed bounds across row navigation. Stop immediately;
# UIAutomator otherwise waits for search callbacks to become idle before dumping.
Tap-WizardText $ui 'Stop searches'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Recommendation 2 of 201'
Assert-WizardContains $ui '1 approved · 0 skipped · 200 remaining'
$paused = Get-SearchStatus $ui
if ($paused -notmatch '^Paused.* (\d+)/201 searched' -or [int]$Matches[1] -ge 201) {
    throw 'Searches did not pause with unfinished rows; active cancellation is unproven.'
}
Tap-WizardText $ui 'Previous'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Staged existing item: Fringe'
Assert-WizardContains $ui '1 approved · 0 skipped · 200 remaining'
Tap-WizardText $ui 'Resume searches'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Staged existing item: Fringe'
if (Find-WizardText $ui 'Stop searches') {
    Tap-WizardText $ui 'Stop searches'
    $ui = Read-WizardUi
}
Assert-WizardContains $ui 'Staged existing item: Fringe'
Tap-WizardText $ui 'Summary'
$ui = Read-WizardUi
$importButton = Find-WizardText $ui 'Import 1 approved recommendations'
if (!$importButton -or $importButton.enabled -ne 'false') { throw 'Import must remain disabled while rows are pending.' }
Tap-WizardText $ui 'Review decisions'
$ui = Read-WizardUi
Tap-WizardText $ui 'Fringe'
$ui = Read-WizardUi
Assert-WizardContains $ui 'Staged existing item: Fringe'
Assert-WizardContains $ui 'Recommendation 1 of 201'
$report = "PASS: 201-row wizard opened progressively; approved Fringe; stopped active searches ($beforeStop); approval survived Previous, Resume and Stop; summary refused undecided rows. No import or database reset was performed."
$report | Set-Content -LiteralPath (Join-Path $EvidencePath 'result.txt') -Encoding utf8
Write-Output $report
