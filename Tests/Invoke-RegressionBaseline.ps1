<#
.SYNOPSIS
    Rebuilds Windows/Android Debug/Release and runs isolated domain/persistence tests.
.DESCRIPTION
    Does not launch/install applications, run legacy smoke scripts or access user databases.
    XAML validation and warnings-as-errors are enabled by the project configuration.
#>
[CmdletBinding()]
param([string] $EvidencePath = (Join-Path ([IO.Path]::GetTempPath()) ("WatchList-Baseline-" + [Guid]::NewGuid().ToString('N')))
    , [string] $AndroidSdkDirectory = ''
    , [switch] $WindowsOnly)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'WatchLists\WatchLists.csproj'
$testsPath = Join-Path $repositoryRoot 'Tests\WatchList.Import.Tests\WatchList.Import.Tests.csproj'
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null

function Invoke-CheckedDotnet([string] $Label, [string[]] $Arguments) {
    $logPath = Join-Path $EvidencePath "$Label.log"
    & dotnet @Arguments *> $logPath
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $logPath -Tail 25 | Write-Host
        throw "Failed: $Label. See $logPath"
    }
    if (Select-String -LiteralPath $logPath -Pattern '(?i)\bwarning\s+[A-Z]+\d+\s*:') {
        throw "Warnings were emitted by $Label. See $logPath"
    }
    Write-Host "Passed: $Label"
}

foreach ($configuration in @('Debug', 'Release')) {
    Invoke-CheckedDotnet "windows-$configuration-restore" @('restore', $projectPath
                                                           , '-p:ValidationPlatformWindowsOnly=true', '-v:minimal')
    $windowsArguments = @($projectPath, '-f', 'net10.0-windows10.0.19041.0', '-c', $configuration
                        , '-p:ValidationPlatformWindowsOnly=true', '-v:minimal')
    Invoke-CheckedDotnet "windows-$configuration-clean" (@('clean') + $windowsArguments)
    Invoke-CheckedDotnet "windows-$configuration" (@('build') + $windowsArguments)
    if (!$WindowsOnly) {
        $androidArguments = @($projectPath, '-f', 'net10.0-android', '-c', $configuration, '-v:minimal')
        if ($AndroidSdkDirectory) { $androidArguments += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
        $restoreArguments = @('restore', $projectPath, '-v:minimal')
        if ($AndroidSdkDirectory) { $restoreArguments += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
        Invoke-CheckedDotnet "android-$configuration-restore" $restoreArguments
        Invoke-CheckedDotnet "android-$configuration-clean" (@('clean') + $androidArguments)
        Invoke-CheckedDotnet "android-$configuration" (@('build') + $androidArguments)
    }
}

Invoke-CheckedDotnet 'domain-tests' @('test', $testsPath, '-c', 'Release', '-v:normal'
                                   , '--logger', 'trx;LogFileName=domain-tests.trx', '--results-directory', $EvidencePath)
Write-Host "Regression baseline passed. Evidence: $EvidencePath"
