<#
.SYNOPSIS
    Produces the README screenshots, on a synthetic backdrop so no real desktop content
    ever ends up in the published images.

.DESCRIPTION
    1. renders a fake "developer desktop" (tools\screenshot-backdrop.py)
    2. shows it full screen in a borderless window
    3. drives QuickShot with injected keyboard/mouse input and screen grabs:
         selection.png         region selection: guides, crosshair, magnifier, size
         snapshots.png         three full size floating snapshots
         collapsed.png         the same three rolled up into thumbnails
         replace-dialog.png    the "snapshot limit reached" chooser
         settings.png          the settings dialog (zh-TW)
    4. writes everything into screenshot\

.EXAMPLE
    powershell -NoProfile -File tools\e2e\screenshots.ps1
#>
[CmdletBinding()]
param(
    [string]$Exe = '',
    [string]$OutDir = '',
    [string]$WorkDir = "$env:TEMP\quickshot-screenshots"
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Exe) { $Exe = Join-Path $projectRoot 'src\QuickShot\bin\Debug\net8.0-windows\QuickShot.exe' }
if (-not $OutDir) { $OutDir = Join-Path $projectRoot 'screenshot' }
$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path (New-Item -ItemType Directory -Force -Path $OutDir)).Path
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$backdropScript = Join-Path $projectRoot 'tools\screenshot-backdrop.py'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace QS -Name Input -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
'@

$screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
$cfgDir = Join-Path $env:APPDATA 'QuickShot'
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
$log = Join-Path $cfgDir 'quickshot.log'

function Save-Screen([string]$name) {
    $bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($screen.Left, $screen.Top, 0, 0, (New-Object System.Drawing.Size($screen.Width, $screen.Height)))
    $g.Dispose()
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("  saved {0}" -f $name)
}

function Set-Config([string]$json) {
    Set-Content -Path (Join-Path $cfgDir 'settings.json') -Value $json -Encoding UTF8
}

function Start-App([string[]]$Arguments = @()) {
    if (Test-Path $log) { Remove-Item $log -Force }
    if ($Arguments.Count -gt 0) { $p = Start-Process $Exe -ArgumentList $Arguments -PassThru }
    else { $p = Start-Process $Exe -PassThru }
    Start-Sleep -Seconds 3
    return $p
}

function Invoke-Hotkey([byte]$key = 0x50) {
    foreach ($m in @(0x11, 0x10)) { [QS.Input]::keybd_event($m, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60 }
    [QS.Input]::keybd_event($key, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
    [QS.Input]::keybd_event($key, 0, 2, [IntPtr]::Zero)
    foreach ($m in @(0x10, 0x11)) { [QS.Input]::keybd_event($m, 0, 2, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 1200
}

function Invoke-Drag([int]$x1, [int]$y1, [int]$x2, [int]$y2, [int]$steps = 8) {
    [QS.Input]::SetCursorPos($x1, $y1) | Out-Null
    Start-Sleep -Milliseconds 160
    [QS.Input]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    for ($i = 1; $i -le $steps; $i++) {
        [QS.Input]::SetCursorPos([int]($x1 + ($x2 - $x1) * $i / $steps), [int]($y1 + ($y2 - $y1) * $i / $steps)) | Out-Null
        Start-Sleep -Milliseconds 50
    }
    [QS.Input]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1300
}

function Stop-Quietly($Process) {
    if ($Process -and -not $Process.HasExited) { Stop-Process -Id $Process.Id -Force; Start-Sleep -Milliseconds 800 }
}

# ---------------------------------------------------------------- backdrop
Write-Host 'rendering the synthetic backdrop'
$backdrop = Join-Path $WorkDir 'backdrop.png'
& py -3.11 $backdropScript --width $screen.Width --height $screen.Height --out $backdrop | Out-Null

$showScript = Join-Path $WorkDir 'show-backdrop.ps1'
@"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
`$img = [System.Drawing.Image]::FromFile('$backdrop')
`$f = New-Object System.Windows.Forms.Form
`$f.FormBorderStyle = 'None'
`$f.StartPosition = 'Manual'
`$f.Bounds = New-Object System.Drawing.Rectangle($($screen.Left), $($screen.Top), $($screen.Width), $($screen.Height))
`$f.TopMost = `$false
`$f.ShowInTaskbar = `$false
`$pb = New-Object System.Windows.Forms.PictureBox
`$pb.Dock = 'Fill'
`$pb.SizeMode = 'Zoom'
`$pb.Image = `$img
`$f.Controls.Add(`$pb)
[System.Windows.Forms.Application]::Run(`$f)
"@ | Set-Content -Path $showScript -Encoding UTF8

$backdropProc = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', $showScript -PassThru
Start-Sleep -Seconds 3

try {
    # ---------------------------------------------------- selection + snapshots
    Write-Host 'shots 1-3: selection, floating snapshots, collapsed'
    Set-Config '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false }'
    $app = Start-App

    # mid-drag: guides, crosshair, magnifier, live size read-out
    Invoke-Hotkey
    [QS.Input]::SetCursorPos(330, 250) | Out-Null
    Start-Sleep -Milliseconds 200
    [QS.Input]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    foreach ($i in 1..10) {
        [QS.Input]::SetCursorPos([int](330 + 62 * $i), [int](250 + 38 * $i)) | Out-Null
        Start-Sleep -Milliseconds 60
    }
    Start-Sleep -Milliseconds 500
    Save-Screen 'selection.png'
    [QS.Input]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1400

    # two more snapshots so the floating set is visible
    Invoke-Hotkey; Invoke-Drag 1230 110 1760 420
    Invoke-Hotkey; Invoke-Drag 1230 600 1760 860
    [QS.Input]::SetCursorPos(660, 980) | Out-Null
    Start-Sleep -Milliseconds 900
    Save-Screen 'snapshots.png'

    # roll them all up
    (Start-Process $Exe -ArgumentList '--collapse' -PassThru).WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 1800
    Save-Screen 'collapsed.png'
    Stop-Quietly $app

    # ---------------------------------------------------- replace dialog
    Write-Host 'shot 4: snapshot limit dialog'
    Set-Config '{ "Hotkey": "Ctrl+Shift+P", "Language": "en", "AutoStart": false, "MaxSnapshots": 2 }'
    $app = Start-App
    Invoke-Hotkey; Invoke-Drag 150 150 760 470
    Invoke-Hotkey; Invoke-Drag 840 150 1450 470
    Invoke-Hotkey; Invoke-Drag 300 620 910 940
    Start-Sleep -Milliseconds 900
    Save-Screen 'replace-dialog.png'
    Stop-Quietly $app

    # ---------------------------------------------------- settings
    Write-Host 'shot 5: settings dialog (zh-TW)'
    Set-Config '{ "Hotkey": "Ctrl+Shift+P", "Language": "zh-TW", "AutoStart": true, "MaxSnapshots": 20 }'
    $app = Start-Process $Exe -ArgumentList '--settings' -PassThru
    Start-Sleep -Seconds 4
    Save-Screen 'settings.png'
    Stop-Quietly $app
}
finally {
    Stop-Quietly $backdropProc
    Remove-Item (Join-Path $cfgDir 'settings.json') -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host 'done. screenshots in:' $OutDir
Get-ChildItem (Join-Path $OutDir '*.png') | ForEach-Object { "  {0,-24} {1,9:N0} bytes" -f $_.Name, $_.Length }
