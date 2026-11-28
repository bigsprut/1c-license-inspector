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
    # Иконка краткого приложения — своя (бирюзовая, с «вкладками»), чтобы в панели задач
    # и в списке процессов два приложения не путались.
    $iconLiteRel = 'res\app-lite.ico'
    if (-not (Test-Path $iconLiteRel)) {
        & (Join-Path $root 'tools\make-icon.ps1') -Variant lite -OutPath (Join-Path $root $iconLiteRel)
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
    # Файл иногда остаётся занятым после предыдущего запуска (антивирус, просмотр файла в
    # проводнике/редакторе): пробуем несколько раз, а если так и не отпустил — собираем
    # рядом (`...new.exe`) и обновляем dist оттуда. Иначе сборка падала на пустом месте.
    $outRel = $exeRel
    for ($i = 0; $i -lt 12; $i++) {
        if (-not (Test-Path $exeRel)) { break }
        try { Remove-Item $exeRel -Force -ErrorAction Stop; break }
        catch { Start-Sleep -Milliseconds 400 }
    }
    if (Test-Path $exeRel) {
        Get-Process '1C-License-Inspector' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
        $canDelete = $false
        try { Remove-Item $exeRel -Force -ErrorAction Stop; $canDelete = $true } catch { }
        if (-not $canDelete) {
            $outRel = 'build\1C-License-Inspector.new.exe'
            Write-Host ('ВНИМАНИЕ: ' + $exeRel + ' занят посторонним процессом — собираю в ' + $outRel)
            if (Test-Path $outRel) { Remove-Item $outRel -Force -ErrorAction SilentlyContinue }
        }
    }

    # Обработчик очереди для удалённого тестирования (C#, без PowerShell).
    # Собираем во временный файл: build\Worker.exe может быть занят запущенным на сервере
    # обработчиком (он берёт exe по UNC-пути). Тогда новый бинарник оставляем рядом
    # как Worker-ready.exe — чтобы его можно было подменить после закрытия окна обработчика,
    # а не пересобирать всё заново.
    if (Test-Path 'tools\Worker.cs') {
        $tmpWorker = 'build\Worker.new.exe'
        Remove-Item 'build\Worker-ready.exe' -Force -ErrorAction SilentlyContinue
        & $csc /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 /out:$tmpWorker tools\Worker.cs
        if ($LASTEXITCODE -eq 0 -and (Test-Path $tmpWorker)) {
            try {
                Move-Item $tmpWorker 'build\Worker.exe' -Force -ErrorAction Stop
            } catch {
                try {
                    Move-Item $tmpWorker 'build\Worker-ready.exe' -Force -ErrorAction Stop
                    Write-Host 'ВНИМАНИЕ: build\Worker.exe занят (обработчик запущен на сервере) — новая сборка в build\Worker-ready.exe, подменить после закрытия окна обработчика.'
                } catch {
                    Write-Host 'ВНИМАНИЕ: build\Worker.exe занят, и Worker-ready.exe тоже не записался — оставляю прежний.'
                    Remove-Item $tmpWorker -Force -ErrorAction SilentlyContinue
                }
            }
        } else {
            throw 'Не удалось собрать Worker.exe'
        }
    }

    # 32-битный помощник (встраивается в приложение ресурсом).
    # Файл может быть занят: его открывает RDP-сессия с сервера (как и Worker.exe), тогда
    # собираем рядом Mmc20Dump-ready.exe и встраиваем его — сам помощник в приложении
    # нужен как ресурс, а на диске он лежит для ручных опытов.
    $helperRel = 'tools\Mmc20Dump.cs'
    $helperBuilt = $false
    $helperPath = 'build\Mmc20Dump.exe'
    if (Test-Path $helperRel) {
        if (Test-Path $helperPath) {
            try { Remove-Item $helperPath -Force -ErrorAction Stop } catch { }
        }
        & $csc /nologo /target:exe /platform:x86 /out:$helperPath $helperRel
        if ($LASTEXITCODE -ne 0) {
            $helperPath = 'build\Mmc20Dump-ready.exe'
            if (Test-Path $helperPath) { Remove-Item $helperPath -Force -ErrorAction SilentlyContinue }
            & $csc /nologo /target:exe /platform:x86 /out:$helperPath $helperRel
            if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать Mmc20Dump.exe' }
            Write-Host 'ВНИМАНИЕ: build\Mmc20Dump.exe занят — собран build\Mmc20Dump-ready.exe' -ForegroundColor Yellow
        }
        $helperBuilt = $true
    }

    $cscArgs = @(
        '/nologo',
        '/target:winexe',
        '/platform:anycpu',
        '/optimize+',
        '/codepage:65001',
        '/warn:4',
        '/main:OneCLicenseInspector.Program',
        ('/out:' + $outRel),
        '/win32manifest:res\app.manifest'
    )
    if (Test-Path $iconRel) { $cscArgs += ('/win32icon:' + $iconRel) }
    if ($helperBuilt) { $cscArgs += ('/resource:' + $helperPath + ',Mmc20Dump.exe') }
    foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
    $cscArgs += $sources

    $log = & $csc $cscArgs 2>&1
    $exit = $LASTEXITCODE
    if (-not $Quiet -or $exit -ne 0) { $log | ForEach-Object { Write-Host $_ } }

    if ($exit -ne 0) { throw ('Сборка не удалась, код ' + $exit) }
    if (-not (Test-Path $outRel)) { throw 'Компилятор не создал exe.' }

    # Краткое приложение: те же исходники, другая точка входа и своё окно (две вкладки).
    $liteRel = 'build\1C-License-Inspector-Lite.exe'
    if (Test-Path $liteRel) {
        for ($i = 0; $i -lt 6; $i++) {
            try { Remove-Item $liteRel -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 300 }
        }
    }
    $liteArgs = @()
    foreach ($a in $cscArgs) {
        if ($a -like '/out:*') { $liteArgs += ('/out:' + $liteRel); continue }
        if ($a -eq '/main:OneCLicenseInspector.Program') { $liteArgs += '/main:OneCLicenseInspector.Lite.LiteProgram'; continue }
        if ($a -eq ('/win32icon:' + $iconRel)) { $liteArgs += ('/win32icon:' + $iconLiteRel); continue }
        $liteArgs += $a
    }
    Get-Process '1C-License-Inspector-Lite' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $logLite = & $csc $liteArgs 2>&1
    $exitLite = $LASTEXITCODE
    if (-not $Quiet -or $exitLite -ne 0) { $logLite | ForEach-Object { Write-Host $_ } }
    if ($exitLite -ne 0) { throw ('Сборка краткого приложения не удалась, код ' + $exitLite) }
    if (-not (Test-Path $liteRel)) { throw 'Компилятор не создал exe краткого приложения.' }

    # Одна сборка — две одинаковые копии: build\ для запуска, dist\ для релиза.
    # Копируем и сверяем SHA-256: 28.11.2026 копии разъехались (1.5.40 против 1.5.42),
    # и можно было запустить старую версию. Теперь расхождение считается провалом сборки.
    $info = Get-Item $outRel
    $ver = $info.VersionInfo.FileVersion
    $distRel = 'dist\1C-License-Inspector.exe'
    $distLiteRel = 'dist\1C-License-Inspector-Lite.exe'
    if (-not (Test-Path 'dist')) { New-Item -ItemType Directory -Path 'dist' -Force | Out-Null }
    try { Copy-Item $outRel $distRel -Force -ErrorAction Stop }
    catch { Write-Host ('ВНИМАНИЕ: не удалось обновить ' + $distRel + ': ' + $_.Exception.Message) }
    try { Copy-Item $liteRel $distLiteRel -Force -ErrorAction Stop }
    catch { Write-Host ('ВНИМАНИЕ: не удалось обновить ' + $distLiteRel + ': ' + $_.Exception.Message) }

    $hashBuild = (Get-FileHash $outRel -Algorithm SHA256).Hash
    $hashDist = if (Test-Path $distRel) { (Get-FileHash $distRel -Algorithm SHA256).Hash } else { '' }
    $same = ($hashBuild -eq $hashDist)
    $hashLite = (Get-FileHash $liteRel -Algorithm SHA256).Hash
    $hashLiteDist = if (Test-Path $distLiteRel) { (Get-FileHash $distLiteRel -Algorithm SHA256).Hash } else { '' }
    $sameLite = ($hashLite -eq $hashLiteDist)
    Write-Host ('Готово: ' + $info.FullName + '  (' + [Math]::Round($info.Length / 1KB) + ' КБ, версия ' + $ver + ')')
    Write-Host ('Копия:  ' + (Join-Path $root $distRel) + ' — ' + $(if ($same) { 'совпадает' } else { 'НЕ СОВПАЛА' }))
    Write-Host ('Кратко: ' + (Join-Path $root $liteRel) + '  (' + [Math]::Round((Get-Item $liteRel).Length / 1KB) + ' КБ)')
    Write-Host ('Копия:  ' + (Join-Path $root $distLiteRel) + ' — ' + $(if ($sameLite) { 'совпадает' } else { 'НЕ СОВПАЛА' }))
    if (-not $same) { throw 'Копия в dist\ не совпала с build\ — сборка не считается готовой.' }
    if (-not $sameLite) { throw 'Копия краткого приложения в dist\ не совпала — сборка не считается готовой.' }
}
finally {
    Pop-Location
}
