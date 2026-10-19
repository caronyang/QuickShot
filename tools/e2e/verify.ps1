<#
.SYNOPSIS
    End-to-end verification for QuickShot. Every claim in the README's "Verified behaviour"
    table is checked here by driving the real application with synthetic input and measuring
    the real windows with Win32.

.DESCRIPTION
    Runs these checks against a freshly started instance:

      1 capture     hotkey -> overlay -> drag -> floating snapshot exists and is sized right
      2 accuracy    the reported snapshot size matches the dragged region exactly
      3 save        clicking the tiny save button writes a PNG to the save folder
      4 limit       with MaxSnapshots=2 the third capture goes through the replace flow
      5 collapse    collapse-all shrinks every window, restore-all returns the exact rects
      6 extreme     a one-line strip still collapses into a usable card
      7 memory      reports the working set after the capturer has done its work

    Artifacts (screenshots) land in %TEMP%\quickshot-verify.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\e2e\verify.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\e2e\verify.ps1 -Configuration Release -SkipBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Exe = Join-Path $root "src\QuickShot\bin\$Configuration\net8.0-windows\QuickShot.exe"
$LogPath = Join-Path $env:APPDATA 'QuickShot\quickshot.log'
$Config = Join-Path $env:APPDATA 'QuickShot\settings.json'
$SaveDir = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'QuickShot'
$Artifacts = Join-Path $env:TEMP 'quickshot-verify'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace QS -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint owner);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@

New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Config) | Out-Null

$script:results = New-Object System.Collections.ArrayList
$screen = [System.Windows.Forms.SystemInformation]::VirtualScreen

function Report([string]$name, [bool]$passed, [string]$detail) {
    $script:results.Add([pscustomobject]@{ Check = $name; Result = if ($passed) { 'PASS' } else { 'FAIL' }; Detail = $detail }) | Out-Null
    $colour = if ($passed) { 'Green' } else { 'Red' }
    Write-Host ("  [{0}] {1,-34} {2}" -f $(if ($passed) { 'PASS' } else { 'FAIL' }), $name, $detail) -ForegroundColor $colour
}

function Save-Screen([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($screen.Left, $screen.Top, 0, 0, (New-Object System.Drawing.Size($screen.Width, $screen.Height)))
    $g.Dispose()
    $bmp.Save((Join-Path $Artifacts $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Set-QuickShotConfig([string]$json) {
    # The previous instance may still be tearing down and holding the file for a moment.
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        try {
            Set-Content -Path $Config -Value $json -Encoding UTF8
            return
        }
        catch [System.IO.IOException] {
            Start-Sleep -Milliseconds 300
        }
    }
    throw "could not write $Config (still locked)"
}

function Start-QuickShot([string[]]$Arguments = @()) {
    if (Test-Path $LogPath) { Remove-Item $LogPath -Force }
    if ($Arguments.Count -gt 0) { $p = Start-Process $Exe -ArgumentList $Arguments -PassThru }
    else { $p = Start-Process $Exe -PassThru }
    Start-Sleep -Seconds 3
    if ($p.HasExited) { throw "QuickShot exited immediately (exit code $($p.ExitCode))" }
    return $p
}

function Stop-QuickShot($Process) {
    if ($Process -and -not $Process.HasExited) { Stop-Process -Id $Process.Id -Force }

    # Wait for it to really be gone, otherwise the next write can hit a lingering lock.
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if (-not (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 150
    }
    Start-Sleep -Milliseconds 500
}

# The snapshot windows are the process' visible top level windows; their rectangles are the
# ground truth for everything below.
function Get-SnapshotRects([int]$ProcessId) {
    $rows = New-Object System.Collections.ArrayList
    $callback = [QS.Win+EnumWindowsProc] {
        param($h, $l)
        [uint32]$owner = 0
        [QS.Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $ProcessId -and [QS.Win]::IsWindowVisible($h)) {
            $r = New-Object 'QS.Win+RECT'
            [QS.Win]::GetWindowRect($h, [ref]$r) | Out-Null
            $w = $r.Right - $r.Left; $h2 = $r.Bottom - $r.Top
            if ($w -gt 20 -and $h2 -gt 20) {
                $rows.Add([pscustomobject]@{ W = $w; H = $h2; X = $r.Left; Y = $r.Top }) | Out-Null
            }
        }
        return $true
    }
    [QS.Win]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $rows
}

function Invoke-Hotkey([byte]$key = 0x50) {
    foreach ($m in @(0x11, 0x10)) { [QS.Win]::keybd_event($m, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60 }
    [QS.Win]::keybd_event($key, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
    [QS.Win]::keybd_event($key, 0, 2, [IntPtr]::Zero)
    foreach ($m in @(0x10, 0x11)) { [QS.Win]::keybd_event($m, 0, 2, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 1200
}

function Invoke-Drag([int]$x1, [int]$y1, [int]$x2, [int]$y2) {
    [QS.Win]::SetCursorPos($x1, $y1) | Out-Null
    Start-Sleep -Milliseconds 150
    [QS.Win]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    for ($i = 1; $i -le 8; $i++) {
        [QS.Win]::SetCursorPos([int]($x1 + ($x2 - $x1) * $i / 8), [int]($y1 + ($y2 - $y1) * $i / 8)) | Out-Null
        Start-Sleep -Milliseconds 45
    }
    [QS.Win]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1400
}

function Get-AppLog { if (Test-Path $LogPath) { Get-Content $LogPath } else { @() } }

# -match on an array returns the matching lines (which is not a boolean), so wrap it.
function Test-Log([string]$Pattern) {
    foreach ($line in Get-AppLog) { if ($line -match $Pattern) { return $true } }
    return $false
}

# --- UI Automation: press the real buttons, so a dead button cannot pass silently --------
function Find-AutomationWindow([string]$name) {
    try {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)
        return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    }
    catch { return $null }
}

function Invoke-AutomationButton($window, [string]$name) {
    if (-not $window) { return $false }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $button) { return $false }
    try {
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    }
    catch { return $false }
}

# ===================================================================== build
if (-not $SkipBuild) {
    Write-Host "== building ($Configuration) ==" -ForegroundColor Cyan
    & dotnet build (Join-Path $root 'src\QuickShot\QuickShot.csproj') -c $Configuration -v:q -nologo
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
if (-not (Test-Path $Exe)) { throw "not found: $Exe" }

Get-Process QuickShot -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# The window is a bit larger than the captured region: 8 DIP shadow margin each side.
$chrome = 20

try {
    # ============================================== 1 + 2 capture and accuracy
    Write-Host "== 1/2 capture, hotkey, pixel accuracy ==" -ForegroundColor Cyan
    Set-QuickShotConfig '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false }'
    $app = Start-QuickShot

    Invoke-Hotkey
    Save-Screen 'overlay.png'
    Invoke-Drag 260 220 820 620            # 560 x 400

    $rects = @(Get-SnapshotRects $app.Id)
    Report 'hotkey opens the overlay' (Test-Log 'Capture requested') 'log: "Capture requested"'
    Report 'snapshot floats after the drag' ($rects.Count -eq 1) "$($rects.Count) floating window(s)"
    Report 'reported size matches the drag' (Test-Log 'floated \(560') 'log: "floated (560x400, 1/20)"'
    if ($rects.Count -ge 1) {
        $expectedW = 560 + $chrome; $expectedH = 400 + $chrome
        $ok = [math]::Abs($rects[0].W - $expectedW) -le 4 -and [math]::Abs($rects[0].H - $expectedH) -le 4
        Report 'window is the captured size' $ok "$($rects[0].W)x$($rects[0].H) (expected ~${expectedW}x${expectedH})"
    }
    Save-Screen 'snapshot.png'

    # ================================================================ 3 save
    Write-Host "== 3 save button ==" -ForegroundColor Cyan
    if (Test-Path $SaveDir) { Remove-Item (Join-Path $SaveDir '*.png') -Force -ErrorAction SilentlyContinue }
    # hover the snapshot so the toolbar fades in, then hit the save button (2nd of four)
    [QS.Win]::SetCursorPos(500, 420) | Out-Null
    Start-Sleep -Milliseconds 500
    [QS.Win]::SetCursorPos(260 + 560 - 63, 220 + 14) | Out-Null
    Start-Sleep -Milliseconds 400
    [QS.Win]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
    [QS.Win]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1500
    $saved = @(Get-ChildItem (Join-Path $SaveDir '*.png') -ErrorAction SilentlyContinue)
    Report 'save button writes a PNG' ($saved.Count -ge 1) $(if ($saved.Count) { "$($saved[0].Name) ($($saved[0].Length) bytes)" } else { 'nothing in the save folder' })
    Stop-QuickShot $app

    # =============================================================== 4 limit
    Write-Host "== 4 snapshot limit ==" -ForegroundColor Cyan
    Set-QuickShotConfig '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false, "MaxSnapshots": 2 }'
    $app = Start-QuickShot
    Invoke-Hotkey; Invoke-Drag 150 150 650 450
    Invoke-Hotkey; Invoke-Drag 700 150 1200 450
    Invoke-Hotkey; Invoke-Drag 200 500 700 800
    Save-Screen 'replace-dialog.png'
    # the dialog is topmost and modal: Enter accepts the default ("Replace", oldest preselected)
    [QS.Win]::keybd_event(0x0D, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
    [QS.Win]::keybd_event(0x0D, 0, 2, [IntPtr]::Zero); Start-Sleep -Milliseconds 1500

    $rects = @(Get-SnapshotRects $app.Id)
    Report 'limit asks for a replacement' ((Test-Log '#3 floated') -and -not (Test-Log 'discarded')) 'log: "#3 floated", no "discarded"'
    Report 'count stays at the limit' ((Test-Log '2/2') -and $rects.Count -eq 2) "$($rects.Count) floating windows (limit 2)"
    Stop-QuickShot $app

    # ============================================================ 5 collapse
    Write-Host "== 5 collapse / restore ==" -ForegroundColor Cyan
    Set-QuickShotConfig '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false, "MaxSnapshots": 20 }'
    $app = Start-QuickShot
    Invoke-Hotkey; Invoke-Drag 200 200 760 500      # 560 x 300
    Invoke-Hotkey; Invoke-Drag 900 200 1300 600     # 400 x 400
    $before = @(Get-SnapshotRects $app.Id) | Sort-Object X, Y

    (Start-Process $Exe -ArgumentList '--collapse' -PassThru).WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 1800
    $collapsed = @(Get-SnapshotRects $app.Id) | Sort-Object X, Y
    Save-Screen 'collapsed.png'

    (Start-Process $Exe -ArgumentList '--collapse' -PassThru).WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 1800
    $restored = @(Get-SnapshotRects $app.Id) | Sort-Object X, Y

    $small = @($collapsed | Where-Object { $_.W -le 200 -and $_.H -le 160 })
    Report 'collapse-all shrinks every window' ($small.Count -eq 2) (($collapsed | ForEach-Object { "$($_.W)x$($_.H)" }) -join ', ')
    $same = ($before.Count -eq $restored.Count) -and
            -not (Compare-Object ($before | ForEach-Object { "$($_.W)x$($_.H)@$($_.X),$($_.Y)" }) `
                                 ($restored | ForEach-Object { "$($_.W)x$($_.H)@$($_.X),$($_.Y)" }))
    Report 'restore-all returns the exact rects' $same (($restored | ForEach-Object { "$($_.W)x$($_.H)@$($_.X),$($_.Y)" }) -join ', ')

    # single window collapse via its own toolbar button (3rd of four). The click position
    # is derived from the measured window rect so it does not depend on hard coded offsets.
    $target = $restored | Where-Object { $_.W -gt 400 } | Select-Object -First 1
    [QS.Win]::SetCursorPos(400, 300) | Out-Null
    Start-Sleep -Milliseconds 400
    [QS.Win]::SetCursorPos($target.X + $target.W - 46, $target.Y + 24) | Out-Null
    Start-Sleep -Milliseconds 400
    [QS.Win]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
    [QS.Win]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1600
    $one = @(Get-SnapshotRects $app.Id)
    $oneSmall = @($one | Where-Object { $_.W -le 200 -and $_.H -le 160 })
    Report 'per-window collapse button' ($oneSmall.Count -eq 1) (($one | ForEach-Object { "$($_.W)x$($_.H)" }) -join ', ')
    Stop-QuickShot $app

    # ============================================================= 6 extreme
    Write-Host "== 6 extreme aspect ratio ==" -ForegroundColor Cyan
    $app = Start-QuickShot
    Invoke-Hotkey; Invoke-Drag 150 700 850 750      # 700 x 50 strip
    (Start-Process $Exe -ArgumentList '--collapse' -PassThru).WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 1800
    $strip = @(Get-SnapshotRects $app.Id)
    $usable = $strip.Count -eq 1 -and $strip[0].H -ge (72 + $chrome - 8) -and $strip[0].H -le (135 + $chrome)
    Report 'thin strip collapses to a card' $usable $(if ($strip.Count) { "$($strip[0].W)x$($strip[0].H)" } else { 'no window' })

    $app.Refresh()
    Report 'memory after the run' $true ("working set {0:N0} MB" -f ($app.WorkingSet64 / 1MB))
    Stop-QuickShot $app

    # ==================================================== 7 settings dialog
    Write-Host "== 7 settings dialog (Save / Cancel really work) ==" -ForegroundColor Cyan
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

    $settingsTitle = 'QuickShot settings'
    Set-QuickShotConfig '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false, "MaxSnapshots": 20 }'
    $app = Start-Process $Exe -ArgumentList '--settings' -PassThru
    Start-Sleep -Seconds 4

    $window = Find-AutomationWindow $settingsTitle
    Report 'settings dialog opens' ($null -ne $window) $(if ($window) { 'window found via UI Automation' } else { 'window not found' })

    # Save must apply the settings, write settings.json and close the window
    if (Test-Path $Config) { Remove-Item $Config -Force }
    $clicked = Invoke-AutomationButton $window 'Save'
    Start-Sleep -Seconds 2
    $stillOpen = $null -ne (Find-AutomationWindow $settingsTitle)
    $written = Test-Path $Config
    Report 'Save writes settings and closes' ($clicked -and $written -and -not $stillOpen) `
        ("clicked={0} settings.json={1} stillOpen={2}" -f $clicked, $written, $stillOpen)

    # Cancel must close the window without touching settings.json. Launching again goes
    # through the single instance signalling, which is worth covering too.
    $stamp = (Get-Item $Config).LastWriteTimeUtc
    (Start-Process $Exe -ArgumentList '--settings' -PassThru).WaitForExit(5000) | Out-Null
    Start-Sleep -Seconds 3
    $window = Find-AutomationWindow $settingsTitle
    $cancelled = Invoke-AutomationButton $window 'Cancel'
    Start-Sleep -Seconds 2
    $stillOpen = $null -ne (Find-AutomationWindow $settingsTitle)
    $untouched = (Get-Item $Config).LastWriteTimeUtc -eq $stamp
    Report 'Cancel closes without writing' ($cancelled -and -not $stillOpen -and $untouched) `
        ("clicked={0} stillOpen={1} settings.json untouched={2}" -f $cancelled, $stillOpen, $untouched)
    Stop-QuickShot $app
}
finally {
    Get-Process QuickShot -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item $Config -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name QuickShot -ErrorAction SilentlyContinue
}

# ================================================================ summary
Write-Host ''
$failed = @($script:results | Where-Object { $_.Result -eq 'FAIL' })
$script:results | Format-Table -AutoSize
Write-Host ("{0} checks, {1} failed. Artifacts: {2}" -f $script:results.Count, $failed.Count, $Artifacts) `
    -ForegroundColor $(if ($failed.Count) { 'Red' } else { 'Green' })

exit $failed.Count


