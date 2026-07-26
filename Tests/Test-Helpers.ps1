# WatchList Regression Test Common Helpers

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

function Take-WindowsScreenshot {
    param(
        [string]$FileNamePrefix,
        [int]$ProcessId = 0
    )
    $tempDir = [System.IO.Path]::GetTempPath()
    $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
    $filePath = Join-Path $tempDir "${FileNamePrefix}_${timestamp}.png"
    
    try {
        # Focus the target window if possible
        try {
            $wshell = New-Object -ComObject WScript.Shell
            if ($ProcessId -gt 0) {
                $wshell.AppActivate($ProcessId) | Out-Null
            } else {
                $wshell.AppActivate("WatchLists") | Out-Null
            }
            Start-Sleep -Milliseconds 1500
        } catch {
            Write-Host "Could not focus application window: $_" -ForegroundColor DarkGray
        }

        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
        Write-Host "Captured failure screenshot: $filePath" -ForegroundColor Yellow
        return $filePath
    } catch {
        Write-Host "Failed to capture screenshot: $_" -ForegroundColor DarkGray
        return $null
    }
}

function Take-AndroidScreenshot {
    param(
        [string]$Device,
        [string]$FileNamePrefix
    )
    $tempDir = [System.IO.Path]::GetTempPath()
    $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
    $localPath = Join-Path $tempDir "${FileNamePrefix}_android_${timestamp}.png"
    
    try {
        $adbArgs = if ($Device) { "-s $Device" } else { "" }
        # Capture screenshot on device
        Start-Process adb -ArgumentList "$adbArgs shell screencap -p /sdcard/screencap.png" -NoNewWindow -Wait
        # Pull screenshot to local machine
        Start-Process adb -ArgumentList "$adbArgs pull /sdcard/screencap.png `"$localPath`"" -NoNewWindow -Wait
        # Remove remote file
        Start-Process adb -ArgumentList "$adbArgs shell rm /sdcard/screencap.png" -NoNewWindow -Wait
        
        Write-Host "Captured Android failure screenshot: $localPath" -ForegroundColor Yellow
        return $localPath
    } catch {
        Write-Host "Failed to capture Android screenshot: $_" -ForegroundColor DarkGray
        return $null
    }
}

function Query-SqliteDatabase {
    param(
        [string]$Query
    )
    $candidates = @(
        "$env:LOCALAPPDATA\Packages\com.companyname.watchlists_8wekyb3d8bbwe\LocalState\watchlist.db"
        "$env:LOCALAPPDATA\WatchLists\watchlist.db"
        "C:\Users\benho\AppData\Local\Data\watchlist.db"
    )
    $dbPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $dbPath) {
        Write-Host "SQLite database not found." -ForegroundColor DarkGray
        return $null
    }
    
    try {
        # Run query using sqlite3 CLI
        $output = & sqlite3 $dbPath $Query
        return $output
    } catch {
        Write-Host "Failed to query SQLite DB: $_" -ForegroundColor Red
        return $null
    }
}

function Clean-SqliteDatabase {
    $candidates = @(
        "$env:LOCALAPPDATA\Packages\com.companyname.watchlists_8wekyb3d8bbwe\LocalState\watchlist.db"
        "$env:LOCALAPPDATA\WatchLists\watchlist.db"
        "C:\Users\benho\AppData\Local\Data\watchlist.db"
    )
    
    foreach ($dbPath in $candidates) {
        if (Test-Path $dbPath) {
            Write-Host "Cleaning database: $dbPath" -ForegroundColor DarkGray
            Get-ChildItem -Path (Split-Path $dbPath) -Filter "watchlist.db*" | Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
}
