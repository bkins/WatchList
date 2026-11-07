param([Parameter(Mandatory)][string]$EvidencePath)

$ErrorActionPreference = 'Stop'
$adbPath = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
$packageName = 'com.companyname.watchlists.enh5acceptance'
$backupPath = 'files/ImportBackups/watchlist-before-muse-20261107-164113-943bdd7169534f049cb80be79bd6f3d1.db'
$preservedPath = 'files/watchlist-before-restore-verification-' + [Guid]::NewGuid().ToString('N') + '.db'
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null

function Invoke-TestAdb {
    param([string[]]$Arguments)
    $result = & $adbPath -s emulator-5554 @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Emulator command failed: $($Arguments -join ' ')" }
    return $result
}

function Export-TestFile {
    param([string]$RemotePath, [string]$LocalName)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $adbPath
    foreach ($argument in @('-s', 'emulator-5554', 'exec-out', 'run-as', $packageName, 'cat', $RemotePath)) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.RedirectStandardOutput = $true
    $startInfo.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($startInfo)
    $outputFile = [IO.File]::Create((Join-Path $EvidencePath $LocalName))
    try { $process.StandardOutput.BaseStream.CopyTo($outputFile) }
    finally { $outputFile.Dispose() }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Disposable test file export failed.' }
}

function Export-TestUi {
    param([string]$LocalName)
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        $dumpResult = Invoke-TestAdb @('shell', 'uiautomator', 'dump', '/sdcard/muse-restoration-check.xml')
        if (("$dumpResult" -notmatch 'ERROR') -and ("$dumpResult" -match 'dumped')) {
            Invoke-TestAdb @('pull', '/sdcard/muse-restoration-check.xml', (Join-Path $EvidencePath $LocalName)) | Out-Null
            return
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'The test app UI did not become available after restart.'
}

$emulator = Invoke-TestAdb @('shell', 'getprop', 'ro.kernel.qemu')
if ("$emulator".Trim() -ne '1') { throw 'This check requires the disposable emulator.' }
Invoke-TestAdb @('shell', 'pm', 'path', $packageName) | Out-Null
$activity = Invoke-TestAdb @('shell', 'cmd', 'package', 'resolve-activity', '--brief', $packageName) |
    Where-Object { $_.StartsWith("$packageName/") } | Select-Object -Last 1
if (!$activity) { throw 'The isolated test activity was not found.' }
Invoke-TestAdb @('shell', 'am', 'force-stop', $packageName) | Out-Null
$files = Invoke-TestAdb @('shell', 'run-as', $packageName, 'ls', 'files')
if ($files -contains 'watchlist.db-wal' -or $files -contains 'watchlist.db-shm') {
    throw 'Unexpected SQLite sidecars; do not overwrite this database.'
}
Export-TestFile 'files/watchlist.db' 'before.db'
Export-TestFile $backupPath 'backup.db'
Invoke-TestAdb @('shell', 'run-as', $packageName, 'cp', 'files/watchlist.db', $preservedPath) | Out-Null
try {
    Invoke-TestAdb @('shell', 'run-as', $packageName, 'cp', $backupPath, 'files/watchlist.db') | Out-Null
    Invoke-TestAdb @('shell', 'am', 'start', '-W', '-n', $activity) | Out-Null
    Export-TestUi 'restored-ui.xml'
    Invoke-TestAdb @('shell', 'am', 'force-stop', $packageName) | Out-Null
    Export-TestFile 'files/watchlist.db' 'restored-after-restart.db'
}
finally {
    Invoke-TestAdb @('shell', 'am', 'force-stop', $packageName) | Out-Null
    Invoke-TestAdb @('shell', 'run-as', $packageName, 'cp', $preservedPath, 'files/watchlist.db') | Out-Null
    Invoke-TestAdb @('shell', 'am', 'start', '-W', '-n', $activity) | Out-Null
    Export-TestUi 'reinstated-ui.xml'
    Invoke-TestAdb @('shell', 'am', 'force-stop', $packageName) | Out-Null
    Export-TestFile 'files/watchlist.db' 'reinstated-after-restart.db'
}
Write-Output 'Restore/restart snapshots captured; compare WatchItems and recommendation records before claiming success.'
