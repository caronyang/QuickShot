$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HK {
    [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
'@

$MOD_ALT = 0x0001
$MOD_CONTROL = 0x0002
$MOD_SHIFT = 0x0004
$MOD_WIN = 0x0008

$tests = @(
    @{ n = 'Ctrl+Shift+P';      m = ($MOD_CONTROL -bor $MOD_SHIFT);                 vk = 0x50 },
    @{ n = 'Ctrl+Shift+Alt+P';  m = ($MOD_CONTROL -bor $MOD_SHIFT -bor $MOD_ALT);   vk = 0x50 },
    @{ n = 'Win+Shift+Down';    m = ($MOD_WIN -bor $MOD_SHIFT);                     vk = 0x28 },
    @{ n = 'Win+Down';          m = $MOD_WIN;                                       vk = 0x28 },
    @{ n = 'Win+Shift+Q';       m = ($MOD_WIN -bor $MOD_SHIFT);                     vk = 0x51 },
    @{ n = 'Win+Shift+S';       m = ($MOD_WIN -bor $MOD_SHIFT);                     vk = 0x53 },
    @{ n = 'Win+L';             m = $MOD_WIN;                                       vk = 0x4C },
    @{ n = 'Ctrl+Shift+S';      m = ($MOD_CONTROL -bor $MOD_SHIFT);                 vk = 0x53 }
)

Write-Output ("OS Build : " + [Environment]::OSVersion.Version.ToString())
Write-Output ("Probe    : " + (Get-Process -Id $PID).ProcessName + " (pid " + $PID + ")")
Write-Output ""

$id = 900
foreach ($t in $tests) {
    $ok = [HK]::RegisterHotKey([IntPtr]::Zero, $id, $t.m, $t.vk)
    $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    if ($ok) { [HK]::UnregisterHotKey([IntPtr]::Zero, $id) | Out-Null }
    $verdict = if ($ok) { 'AVAILABLE' } else { 'BLOCKED' }
    $errText = switch ($err) { 1409 { 'ERROR_HOTKEY_ALREADY_REGISTERED' } 0 { '' } default { 'Win32Error=' + $err } }
    Write-Output ('{0,-18} {1,-10} {2} {3}' -f $t.n, $verdict, $errText, '')
    $id++
}
