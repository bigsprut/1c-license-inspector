# Сборка «Инспектор лицензий 1С» компилятором .NET Framework 4.0 (csc.exe).
# Никаких внешних зависимостей: на выходе один exe, работающий на Windows Server 2003 x64
# (при установленном .NET Framework 4.0) и на Windows Server 2016/2019/2022.
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root 'build'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'Не найден компилятор C# 4.0 (csc.exe). Установите .NET Framework 4.x.' }

$icon = Join-Path $root 'res\app.ico'
if (-not (Test-Path $icon)) {
    & (Join-Path $root 'tools\make-icon.ps1')
}

$sources = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw 'Не найдены файлы исходного кода.' }

$netDir = Split-Path -Parent $csc
$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll',
    'System.Management.dll',
    'System.ServiceProcess.dll'
) | ForEach-Object { Join-Path $netDir $_ }

$exe = Join-Path $outDir '1C-License-Inspector.exe'
$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/warn:4',
    ('/out:"' + $exe + '"'),
    ('/win32manifest:"' + (Join-Path $root 'res\app.manifest') + '"')
)
if (Test-Path $icon) { $args += ('/win32icon:"' + $icon + '"') }
foreach ($r in $refs) { $args += ('/reference:"' + $r + '"') }
$args += $sources

Get-Process '1C-License-Inspector' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
if (Test-Path $exe) { Remove-Item $exe -Force }

$log = & $csc $args 2>&1
$exit = $LASTEXITCODE
if (-not $Quiet -or $exit -ne 0) { $log | ForEach-Object { Write-Host $_ } }

if ($exit -ne 0) { throw ('Сборка не удалась, код ' + $exit) }
if (-not (Test-Path $exe)) { throw 'Компилятор не создал exe.' }

$info = Get-Item $exe
Write-Host ('Готово: ' + $info.FullName + '  (' + [Math]::Round($info.Length / 1KB) + ' КБ)')
