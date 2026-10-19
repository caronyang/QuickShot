<#
.SYNOPSIS
    Publishes QuickShot and (optionally) builds the installer.

.DESCRIPTION
    Two flavours:

      default (self-contained single file, ~63 MB)
          One QuickShot.exe that runs on any Windows 10/11 x64 machine with no .NET
          runtime installed. Costs a chunk of RAM: the bundle is extracted and the whole
          runtime is mapped into the process.

      -Framework (framework-dependent, ~1 MB)
          Much lighter at runtime (~80 MB working set) but requires the .NET 8 Desktop
          Runtime on the target machine.

    The installer installs per user into %LocalAppData%\Programs\QuickShot, so it never
    asks for administrator rights.

.EXAMPLE
    .\build\publish.ps1
    .\build\publish.ps1 -Framework -SkipInstaller
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$Framework,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\QuickShot\QuickShot.csproj'
$iss = Join-Path $root 'installer\QuickShot.iss'
$publishDir = Join-Path $root $(if ($Framework) { 'build\publish-framework' } else { 'build\publish' })

$flavour = if ($Framework) { 'framework-dependent' } else { 'self-contained single file' }
Write-Host "== QuickShot publish ($Configuration / $Runtime / $flavour) ==" -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$args = @($project, '-c', $Configuration, '-o', $publishDir, '-v:m', '-nologo',
    '-p:DebugType=none', '-p:GenerateDocumentationFile=false')

if ($Framework) {
    $args += @('-p:PublishSingleFile=false', '-p:SelfContained=false')
}
else {
    $args += @('-r', $Runtime, '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true')
}

dotnet publish @args
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $publishDir 'QuickShot.exe'
if (-not (Test-Path $exe)) { throw "QuickShot.exe was not produced" }
Write-Host ("published: {0} ({1:N1} MB)" -f $exe, ((Get-Item $exe).Length / 1MB)) -ForegroundColor Green

if ($SkipInstaller) { return }

$iscc = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning 'Inno Setup 6 (ISCC.exe) not found - skipping the installer step.'
    return
}

# Inno Setup 6.4 and earlier bundle the unofficial translations; 6.5+ do not. Offer the
# Traditional Chinese installer UI only when the language file is actually installed.
$tradIsl = Join-Path (Split-Path -Parent $iscc) 'Languages\ChineseTrad.isl'
$isccArgs = @()
if (Test-Path $tradIsl) {
    $isccArgs += '/DUseChineseTrad'
    Write-Host 'installer languages: english + chinesetraditional'
}
else {
    Write-Host 'installer languages: english (ChineseTrad.isl not installed)' -ForegroundColor Yellow
}
$isccArgs += "/DPublishDir=$publishDir"
$isccArgs += $iss

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

Get-ChildItem (Join-Path $root 'build\installer\*.exe') | ForEach-Object {
    Write-Host ("installer: {0} ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) -ForegroundColor Green
}
