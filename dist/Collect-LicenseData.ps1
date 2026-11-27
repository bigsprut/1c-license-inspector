#requires -Version 3.0
<#
    Collect-LicenseData.ps1
    Сбор сведений о лицензиях 1С:Предприятие на компьютере.
    ТОЛЬКО ЧТЕНИЕ: скрипт ничего не устанавливает, не активирует и не меняет.
    Запускать на сервере, где есть программные лицензии, от имени администратора.

    Результат: текстовый файл (по умолчанию на Рабочем столе) — его можно проверить
    глазами и передать разработчику.

    Параметры:
      -OutPath      куда сохранить отчёт (по умолчанию Desktop\1c-license-data.txt)
      -IncludeHex   добавить первые 64 байта каждого .lic в hex (по умолчанию выключено)
      -SkipRing     не запускать утилиту лицензирования ring
#>
param(
    [string]$OutPath = (Join-Path ([Environment]::GetFolderPath('Desktop')) '1c-license-data.txt'),
    [switch]$IncludeHex,
    [switch]$SkipRing
)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

$script:sb = New-Object System.Text.StringBuilder

function W([string]$text) {
    [void]$script:sb.AppendLine($text)
    Write-Host $text
}
function H1([string]$text) {
    W ''
    W ('=' * 78)
    W $text
    W ('=' * 78)
}
function H2([string]$text) {
    W ''
    W ('--- ' + $text + ' ' + ('-' * [Math]::Max(0, 70 - $text.Length)))
}
function Try-Get([scriptblock]$block, [string]$errorPrefix) {
    try { & $block }
    catch { W ($errorPrefix + ': ' + $_.Exception.Message) }
}

# ---------------------------------------------------------------- заголовок
H1 'СВЕДЕНИЯ О ЛИЦЕНЗИЯХ 1С'
W ('Дата сбора       : ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
W ('Компьютер        : ' + $env:COMPUTERNAME)
W ('Пользователь     : ' + $env:USERDOMAIN + '\' + $env:USERNAME)
W ('PowerShell       : ' + $PSVersionTable.PSVersion.ToString())

H2 'Операционная система'
Try-Get {
    $os = Get-CimInstance Win32_OperatingSystem
    W ('Название         : ' + $os.Caption)
    W ('Версия / сборка  : ' + $os.Version + ' / ' + $os.BuildNumber)
    W ('Разрядность      : ' + $os.OSArchitecture)
    W ('Установлена      : ' + $os.InstallDate)
    W ('Процессор        : ' + (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name))
    W ('Память, МБ       : ' + [Math]::Round($os.TotalVisibleMemorySize / 1024))
} 'ОС'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
W ('Администратор    : ' + $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))

# ---------------------------------------------------------------- платформы
H1 'ПЛАТФОРМЫ 1С:ПРЕДПРИЯТИЕ'

H2 'Установленные компоненты (реестр Uninstall)'
Try-Get {
    $roots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    Get-ItemProperty $roots |
        Where-Object { $_.DisplayName -match '1С|1C' -and $_.DisplayName -match 'Предприятие|Enterprise|1cv' } |
        Sort-Object DisplayName -Unique |
        ForEach-Object { W ('{0,-58} {1,-14} {2}' -f $_.DisplayName, $_.DisplayVersion, $_.InstallLocation) }
} 'Uninstall'

H2 'Каталоги платформ'
Try-Get {
    $dirs = @()
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $base) { continue }
        foreach ($name in @('1cv8', '1cv82', '1cv8fv')) {
            $p = Join-Path $base $name
            if (Test-Path $p) {
                Get-ChildItem $p -Directory | Where-Object { $_.Name -match '^\d+\.\d+' } | ForEach-Object {
                    W ('{0}   {1}' -f $_.FullName, $_.LastWriteTime)
                }
            }
        }
    }
    $p77 = Join-Path ${env:ProgramFiles(x86)} '1Cv77'
    if (Test-Path $p77) { W ('1Cv77: ' + $p77 + '   ' + (Get-Item $p77).LastWriteTime) }
} 'Каталоги'

# ---------------------------------------------------------------- файлы лицензий
H1 'ФАЙЛЫ ПРОГРАММНЫХ ЛИЦЕНЗИЙ (*.lic)'

$licenseDirs = @(
    (Join-Path $env:ProgramData '1C\licenses'),
    (Join-Path $env:ProgramData '1C\1CE'),
    (Join-Path $env:APPDATA '1C\licenses'),
    (Join-Path $env:LOCALAPPDATA '1C\licenses'),
    (Join-Path $env:ProgramData '1C\1Cv82')
)

$licFiles = @()
foreach ($d in $licenseDirs) {
    if (Test-Path $d) {
        $found = Get-ChildItem $d -Recurse -Filter '*.lic' -File
        W ('Каталог: ' + $d + '   файлов: ' + @($found).Count)
        foreach ($f in $found) { $licFiles += $f }
    }
    else {
        W ('Каталог отсутствует: ' + $d)
    }
}

H2 'Идентификатор компьютера (привязка лицензий)'
foreach ($p in @(
        (Join-Path $env:ProgramData '1C\1cv8\host_id'),
        (Join-Path $env:ProgramData '1C\1CE\host_id'))) {
    if (Test-Path $p) {
        W ('Файл: ' + $p)
        W (Get-Content $p -TotalCount 1)
    }
}

H2 'Перечень файлов'
if (@($licFiles).Count -eq 0) {
    W 'Файлы *.lic не найдены.'
}
foreach ($f in $licFiles) {
    $hash = (Get-FileHash $f.FullName -Algorithm SHA1).Hash
    W ''
    W ('Файл      : ' + $f.FullName)
    W ('Размер    : ' + $f.Length + ' байт')
    W ('Изменён   : ' + $f.LastWriteTime)
    W ('SHA1      : ' + $hash)
    if ($IncludeHex) {
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        $n = [Math]::Min(64, $bytes.Length)
        $hex = ($bytes[0..($n - 1)] | ForEach-Object { $_.ToString('X2') }) -join ' '
        $asc = -join ($bytes[0..($n - 1)] | ForEach-Object { if ($_ -ge 32 -and $_ -lt 127) { [char]$_ } else { '.' } })
        W ('HEX       : ' + $hex)
        W ('ASCII     : ' + $asc)
    }
}

# ---------------------------------------------------------------- ring
H1 'УТИЛИТА ЛИЦЕНЗИРОВАНИЯ 1С (ring)'

if ($SkipRing) {
    W 'Пропущено по параметру -SkipRing.'
}
else {
    $ringCandidates = @()
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData)) {
        if (-not $base) { continue }
        foreach ($sub in @('1C\1CE\components', '1C\1CE', '1C\Enterprise License Tools', '1C')) {
            $p = Join-Path $base $sub
            if (Test-Path $p) {
                Get-ChildItem $p -Recurse -Include 'ring.exe', 'ring.bat', 'ring.cmd' -File -Depth 3 | ForEach-Object { $ringCandidates += $_.FullName }
            }
        }
    }
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $base) { continue }
        foreach ($name in @('1cv8', '1cv82')) {
            $p = Join-Path $base $name
            if (-not (Test-Path $p)) { continue }
            Get-ChildItem $p -Directory | ForEach-Object {
                foreach ($rel in @('ring.exe', 'ExtDst\ring.exe', 'ExtDst\ring.bat', 'bin\ring.exe')) {
                    $cand = Join-Path $_.FullName $rel
                    if (Test-Path $cand) { $ringCandidates += $cand }
                }
            }
        }
    }
    $ringCmd = Get-Command ring -ErrorAction SilentlyContinue

    W ('Java в PATH   : ' + [bool](Get-Command java -ErrorAction SilentlyContinue))
    W ('Найдено ring  : ' + (@($ringCandidates | Sort-Object -Unique) -join '; '))
    if ($ringCmd) { W ('ring в PATH   : ' + $ringCmd.Source) }

    $ring = @($ringCandidates | Sort-Object -Unique | Select-Object -First 1)
    if ($ring.Count -eq 0 -and $ringCmd) { $ring = @($ringCmd.Source) }

    if ($ring.Count -eq 0) {
        W 'Утилита ring не найдена. Её можно поставить отдельно (1C:Enterprise License Tools).'
    }
    else {
        $ringExe = $ring[0]
        $outFile = [System.IO.Path]::GetTempFileName()
        $errFile = [System.IO.Path]::GetTempFileName()

        function Invoke-Ring([string[]]$arguments, [string]$title) {
            W ''
            W ('>> ' + $title)
            Try-Get {
                $p = Start-Process -FilePath $ringExe -ArgumentList $arguments -NoNewWindow -PassThru `
                    -RedirectStandardOutput $outFile -RedirectStandardError $errFile
                if (-not $p.WaitForExit(20000)) {
                    try { $p.Kill() } catch { }
                    W '[ring] не ответил за 20 секунд'
                }
                W ('[exit code] ' + $p.ExitCode)
                if (Test-Path $outFile) { Get-Content $outFile | ForEach-Object { W ('  ' + $_) } }
                if ((Get-Item $errFile).Length -gt 0) {
                    W '--- stderr:'
                    Get-Content $errFile | ForEach-Object { W ('  ' + $_) }
                }
            } 'ring'
        }

        Invoke-Ring @('--version') 'ring --version'
        Invoke-Ring @('license', 'list') 'ring license list'

        foreach ($d in $licenseDirs) {
            if (-not (Test-Path $d)) { continue }
            Invoke-Ring @('license', 'list', '--path', $d) ('ring license list --path "' + $d + '"')
        }

        Remove-Item $outFile, $errFile -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------- реестр
H1 'РЕЕСТР: ВЕТКИ 1С'

foreach ($root in @('HKLM:\SOFTWARE\1C', 'HKLM:\SOFTWARE\WOW6432Node\1C')) {
    H2 $root
    if (-not (Test-Path $root)) { W 'нет'; continue }

    $keys = @(Get-ChildItem $root -Recurse -Depth 2 -ErrorAction SilentlyContinue)
    W ('Ключей (до глубины 2): ' + $keys.Count)
    $shown = 0
    foreach ($key in $keys) {
        if ($shown -ge 120) { W '… список сокращён'; break }
        W ('[' + $key.Name + ']')
        $shown++
        if ($key.Name -match 'licen|serial|pin') {
            foreach ($v in $key.GetValueNames()) { W ('    ' + $v + ' = ' + $key.GetValue($v)) }
        }
    }
    $licenKeys = @($keys | Where-Object { $_.Name -match 'licen' })
    if ($licenKeys.Count -eq 0) { W 'Ключей со словом "licen" не найдено.' }
}

H2 'Ключи HASP / Aladdin / SafeNet'
foreach ($root in @('HKLM:\SOFTWARE\Aladdin', 'HKLM:\SOFTWARE\WOW6432Node\Aladdin', 'HKLM:\SOFTWARE\SafeNet', 'HKLM:\SYSTEM\CurrentControlSet\Services\hasplms', 'HKLM:\SYSTEM\CurrentControlSet\Services\HASP Loader')) {
    if (Test-Path $root) {
        W ('[' + $root + ']')
        $item = Get-Item $root
        foreach ($v in $item.GetValueNames()) { W ('    ' + $v + ' = ' + $item.GetValue($v)) }
    }
}

# ---------------------------------------------------------------- службы / порты
H1 'СЛУЖБЫ И ПОРТЫ'

H2 'Службы защиты'
Try-Get {
    Get-Service | Where-Object { $_.Name -match 'hasp|sentinel|aladdin|1cv8|1c' -or $_.DisplayName -match 'HASP|Sentinel|1С' } |
        Sort-Object Name |
        ForEach-Object { W ('{0,-28} {1,-12} {2,-10} {3}' -f $_.Name, $_.Status, $_.StartType, $_.DisplayName) }
} 'Службы'

H2 'Прослушиваемые порты'
Try-Get {
    Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalPort -in 475, 1947, 1540, 1541, 1545, 1560, 1561, 1562, 1433, 5432 } |
        ForEach-Object {
            $proc = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
            W ('{0,-6} {1,-22} PID {2,-6} {3}' -f $_.LocalPort, $_.LocalAddress, $_.OwningProcess, $(if ($proc) { $proc.ProcessName } else { '' }))
        }
} 'Порты'

# ---------------------------------------------------------------- Sentinel ACC
H1 'МЕНЕДЖЕР ЛИЦЕНЗИЙ SENTINEL LDK (порт 1947)'

Try-Get {
    $body = '<admin><hasp><element name="*"/></hasp></admin>'
    $r = Invoke-RestMethod -Uri 'http://127.0.0.1:1947/_int_/adminapi/get' -Method Post -Body $body -ContentType 'text/xml' -TimeoutSec 5
    W 'Ответ на запрос ключей:'
    W ($r.OuterXml)
} 'Sentinel keys'

Try-Get {
    $body = '<admin><feature><element name="*"/></feature></admin>'
    $r = Invoke-RestMethod -Uri 'http://127.0.0.1:1947/_int_/adminapi/get' -Method Post -Body $body -ContentType 'text/xml' -TimeoutSec 5
    H2 'Компоненты (features)'
    W ($r.OuterXml)
} 'Sentinel features'

# ---------------------------------------------------------------- rac
H1 'СЕАНСЫ 1С (rac, если сервер доступен)'

Try-Get {
    $rac = $null
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $base) { continue }
        $p = Join-Path $base '1cv8'
        if (Test-Path $p) {
            $cand = Get-ChildItem $p -Recurse -Filter 'rac.exe' -File -Depth 3 | Sort-Object FullName -Descending | Select-Object -First 1
            if ($cand) { $rac = $cand.FullName; break }
        }
    }
    if (-not $rac) { W 'rac.exe не найден'; return }
    W ('rac.exe: ' + $rac)

    $listen = Get-NetTCPConnection -State Listen -LocalPort 1545 -ErrorAction SilentlyContinue
    if (-not $listen) { W 'Порт 1545 (ras) не прослушивается — пропускаем.'; return }

    $clusters = & $rac 127.0.0.1:1545 cluster list 2>&1
    W '--- cluster list:'
    $clusters | ForEach-Object { W ('  ' + $_) }

    foreach ($line in $clusters) {
        if ($line -match 'cluster\s*:\s*([0-9a-f\-]{36})') {
            $uuid = $Matches[1]
            W ('--- sessions for cluster ' + $uuid + ' (--licenses):')
            & $rac 127.0.0.1:1545 session list --licenses --cluster=$uuid 2>&1 | ForEach-Object { W ('  ' + $_) }
            break
        }
    }
} 'rac'

# ---------------------------------------------------------------- сохранение
H1 'ГОТОВО'
W ('Размер отчёта: ' + $script:sb.Length + ' символов')

try {
    $dir = Split-Path -Parent $OutPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($OutPath, $script:sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))
    Write-Host ''
    Write-Host ('Отчёт сохранён: ' + $OutPath) -ForegroundColor Green
}
catch {
    Write-Host ('Не удалось сохранить отчёт: ' + $_.Exception.Message) -ForegroundColor Red
}
