# Сборка «Инспектор лицензий 1С» компилятором .NET Framework 4.0 (csc.exe).
# Никаких внешних зависимостей: на выходе один exe, работающий на Windows Server 2003 x64
# (при установленном .NET Framework 4.0) и на Windows Server 2016/2019/2022.
#
# Важно: все пути компилятору передаются относительными и без кавычек — иначе PowerShell 7
# на GitHub Actions передаёт кавычки буквально, и csc падает с CS2021.
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

Push-Location $root
try {
    $iconRel = 'res\app.ico'
    if (-not (Test-Path $iconRel)) {
        & (Join-Path $root 'tools\make-icon.ps1') -OutPath (Join-Path $root $iconRel)
    }

    $sources = Get-ChildItem 'src' -Recurse -Filter *.cs | ForEach-Object { $_.FullName.Substring($root.Length + 1) }
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
        'System.ServiceProcess.dll',
        'System.Security.dll'
    ) | ForEach-Object { Join-Path $netDir $_ }

    Get-Process '1C-License-Inspector' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600

    $exeRel = 'build\1C-License-Inspector.exe'
    if (Test-Path $exeRel) { Remove-Item $exeRel -Force }

    # 32-битный помощник (встраивается в приложение ресурсом)
    $helperRel = 'tools\Mmc20Dump.cs'
    $helperBuilt = $false
    if (Test-Path $helperRel) {
        & $csc /nologo /target:exe /platform:x86 /out:build\Mmc20Dump.exe $helperRel
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать Mmc20Dump.exe' }
        $helperBuilt = $true
    }

    $cscArgs = @(
        '/nologo',
        '/target:winexe',
        '/platform:anycpu',
        '/optimize+',
        '/codepage:65001',
        '/warn:4',
        ('/out:' + $exeRel),
        '/win32manifest:res\app.manifest'
    )
    if (Test-Path $iconRel) { $cscArgs += ('/win32icon:' + $iconRel) }
    if ($helperBuilt) { $cscArgs += '/resource:build\Mmc20Dump.exe,Mmc20Dump.exe' }
    foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
    $cscArgs += $sources

    $log = & $csc $cscArgs 2>&1
    $exit = $LASTEXITCODE
    if (-not $Quiet -or $exit -ne 0) { $log | ForEach-Object { Write-Host $_ } }

    if ($exit -ne 0) { throw ('Сборка не удалась, код ' + $exit) }
    if (-not (Test-Path $exeRel)) { throw 'Компилятор не создал exe.' }



    $info = Get-Item $exeRel
    Write-Host ('Готово: ' + $info.FullName + '  (' + [Math]::Round($info.Length / 1KB) + ' КБ)')
}
finally {
    Pop-Location
}
