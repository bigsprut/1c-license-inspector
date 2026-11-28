# Обработчик очереди задач для «Инспектора лицензий 1С» (сервер 2003 или 2016).
# Запускается ВНУТРИ RDP-сеанса на сервере: видит перенаправленную папку \\tsclient\...
# Выполняет ТОЛЬКО белый список команд из queue\<имя>. Результат кладёт в drop\.
#
# Совместим с PowerShell 2.0 (Server 2003): без $PSScriptRoot, Get-FileHash и т.п.
#
# Запуск:  powershell -ExecutionPolicy Bypass -File tools\server-worker.ps1 -Name all
#          powershell -ExecutionPolicy Bypass -File tools\server-worker.ps1 -Name s200
param(
    [string]$Name = "all",
    [string]$Root = "",
    [int]$IntervalMs = 1500
)

$ErrorActionPreference = "Continue"

if ($Root -eq "") {
    $Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
}
$queue = Join-Path $Root "queue\$Name"
$done = Join-Path $queue "done"
$drop = Join-Path $Root "drop"
$app = Join-Path $Root "build\1C-License-Inspector.exe"
$work = Join-Path $env:TEMP "1c-inspector-worker"
$local = Join-Path $work "1cli.exe"
$heartbeat = Join-Path $drop ("worker-" + $Name + ".txt")
$log = Join-Path $drop ("worker-" + $Name + ".log")

foreach ($d in @($queue, $done, $drop, $work)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

function Write-Log([string]$text) {
    $line = (Get-Date).ToString("HH:mm:ss") + "  " + $text
    try { Add-Content -Path $log -Value $line -Encoding UTF8 } catch { }
}

# Запуск процесса с ожиданием через .NET: без вложенного cmd и без окна,
# поэтому ничего не «зависает» на ожидании ввода (поймано на живом тесте).
function Run-Wait([string]$exe, [string]$args, [string]$outFile) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = $args
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $capture = ($outFile -ne "")
    # Перенаправляем потоки ТОЛЬКО когда нужен текст: если приложение (winexe) или его
    # дочерние процессы держат унаследованный pipe, ReadToEnd ждёт EOF и «висит».
    $psi.RedirectStandardOutput = $capture
    $psi.RedirectStandardError = $capture
    $p = [System.Diagnostics.Process]::Start($psi)
    if ($capture) {
        $out = $p.StandardOutput.ReadToEnd() + $p.StandardError.ReadToEnd()
        $p.WaitForExit()
        [System.IO.File]::WriteAllText($outFile, $out, [System.Text.Encoding]::Default)
    }
    else {
        if (-not $p.WaitForExit(120000)) { try { $p.Kill() } catch { } ; return -9 }
    }
    return $p.ExitCode
}

# Запуск приложения без ожидания выхода: COM/HTTP-потоки могут держать процесс,
# поэтому ждём появления файла результата, а процесс потом гасим.
function Start-App([string]$args) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $local
    $psi.Arguments = $args
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    return [System.Diagnostics.Process]::Start($psi)
}

function Wait-Path([string]$path, [int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $path) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return (Test-Path $path)
}
# Копия приложения локально: с 2003 exe из UNC-папки может не загрузиться,
# а так всегда берётся свежая сборка.
function Sync-App() {
    if (-not (Test-Path $app)) { return "нет файла сборки: $app" }
    $need = $true
    if (Test-Path $local) {
        $a = Get-Item $app
        $b = Get-Item $local
        if ($a.Length -eq $b.Length -and $a.LastWriteTime -eq $b.LastWriteTime) { $need = $false }
    }
    if ($need) {
        Copy-Item $app $local -Force
        Write-Log "обновлена локальная копия сборки"
    }
    return ""
}

function Get-AppLogPath {
    $p1 = Join-Path $env:TEMP "1c-license-inspector.log"
    if (Test-Path $p1) { return $p1 }
    $p2 = Join-Path $work "1c-license-inspector.log"
    if (Test-Path $p2) { return $p2 }
    return ""
}

# Подчистить зависшие экземпляры (например, после проверки GUI в фоновом сеансе):
# они держат локальную копию сборки и мешают её обновлять.
try {
    Get-Process 1cli -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Log ("снимаю зависший экземпляр: pid=" + $_.Id)
        try { $_.Kill() } catch { }
    }
} catch { }

Write-Log ("запуск обработчика: имя=" + $Name + ", корень=" + $Root)
Set-Content -Path $heartbeat -Value ((Get-Date).ToString("s") + " · " + $env:COMPUTERNAME + " · " + $Name) -Encoding UTF8

while ($true) {
    try {
        Set-Content -Path $heartbeat -Value ((Get-Date).ToString("s") + " · " + $env:COMPUTERNAME + " · " + $Name) -Encoding UTF8

        $task = Get-ChildItem -Path $queue -Filter "*.task" -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -First 1
        if ($task -eq $null) { Start-Sleep -Milliseconds $IntervalMs; continue }

        $id = [System.IO.Path]::GetFileNameWithoutExtension($task.Name)
        # ReadAllText, а не Get-Content: файл из одной строки возвращается строкой,
        # и $lines[0] дал бы первую БУКВУ команды (поймано на живом тесте).
        $text = [System.IO.File]::ReadAllText($task.FullName)
        $command = $text.Trim()
        if ($command.IndexOf("`n") -ge 0) { $command = $command.Split("`n")[0] }
        $command = $command.Trim().ToLower()
        $resultFile = Join-Path $drop ("result-" + $Name + "-" + $id + ".txt")
        $body = New-Object System.Collections.ArrayList
        [void]$body.Add("Задание: " + $id + " · команда: " + $command)
        [void]$body.Add("Сервер: " + $env:COMPUTERNAME + " · " + (Get-Date).ToString("dd.MM.yyyy HH:mm:ss"))
        [void]$body.Add("")

        Write-Log ("задание " + $id + ": " + $command)

        switch ($command) {
            "noop" {
                [void]$body.Add("Обработчик на связи.")
            }
            "digest" {
                $err = Sync-App
                if ($err -ne "") { [void]$body.Add($err) }
                else {
                    # Пишем отчёт в ЛОКАЛЬНЫЙ файл (UNC-запись из приложения иногда висит),
                    # ждём появления и копируем в drop. Выход процесса не важен.
                    $tmpOut = Join-Path $work ("digest-" + $id + ".txt")
                    if (Test-Path $tmpOut) { Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue }
                    $proc = Start-App ("/digest:`"" + $tmpOut + "`" /silent")
                    $ok = Wait-Path $tmpOut 240
                    Start-Sleep -Seconds 1
                    try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }
                    if ($ok) {
                        $dst = Join-Path $drop ("digest-" + $Name + "-" + $id + ".txt")
                        Copy-Item $tmpOut $dst -Force
                        [void]$body.Add("готово: " + $dst + " (" + (Get-Item $dst).Length + " байт)")
                    }
                    else { [void]$body.Add("ФАЙЛ НЕ ПОЯВИЛСЯ за 240 с") }
                }
            }
            "navtest" {
                $err = Sync-App
                if ($err -ne "") { [void]$body.Add($err) }
                else {
                    $logFile = Get-AppLogPath
                    if ($logFile -ne "" -and (Test-Path $logFile)) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }
                    $rc = Run-Wait $local "/navtest /noerrorbox" ""
                    Start-Sleep -Seconds 2
                    $copy = Join-Path $drop ("navtest-" + $Name + "-" + $id + ".log")
                    $logFile = Get-AppLogPath
                    if ($logFile -ne "") { Copy-Item $logFile $copy -Force; [void]$body.Add("лог: " + $copy) }
                    [void]$body.Add("rc=" + $rc)
                }
            }
            "racprobe" {
                $err = Sync-App
                if ($err -ne "") { [void]$body.Add($err) }
                else {
                    $tmpOut = Join-Path $work ("racprobe-" + $id + ".txt")
                    if (Test-Path $tmpOut) { Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue }
                    $proc = Start-App ("/racprobe:`"" + $tmpOut + "`" /silent")
                    $ok = Wait-Path $tmpOut 180
                    Start-Sleep -Seconds 1
                    try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }
                    if ($ok) {
                        $dst = Join-Path $drop ("racprobe-" + $Name + "-" + $id + ".txt")
                        Copy-Item $tmpOut $dst -Force
                        [void]$body.Add("готово: " + $dst + " (" + (Get-Item $dst).Length + " байт)")
                    }
                    else { [void]$body.Add("ФАЙЛ НЕ ПОЯВИЛСЯ за 180 с") }
                }
            }
            "selftest" {
                $err = Sync-App
                if ($err -ne "") { [void]$body.Add($err) }
                else {
                    $tmpOut = Join-Path $work ("selftest-" + $id + ".txt")
                    if (Test-Path $tmpOut) { Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue }
                    $proc = Start-App ("/selftest /selftestout:`"" + $tmpOut + "`" /silent")
                    $ok = Wait-Path $tmpOut 180
                    Start-Sleep -Seconds 1
                    try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }
                    if ($ok) {
                        $dst = Join-Path $drop ("selftest-" + $Name + "-" + $id + ".txt")
                        Copy-Item $tmpOut $dst -Force
                        [void]$body.Add("готово: " + $dst)
                        foreach ($l in (Get-Content $tmpOut -Encoding UTF8)) { [void]$body.Add("  " + $l) }
                    }
                    else { [void]$body.Add("ФАЙЛ НЕ ПОЯВИЛСЯ за 180 с") }
                }
            }
            "netstat" {
                $out = Join-Path $work "netstat.txt"
                $rc = Run-Wait "cmd.exe" ('/c netstat -an | findstr ":1540 :1541 :1545"') $out
                [void]$body.Add("rc=" + $rc)
                if (Test-Path $out) { foreach ($l in (Get-Content $out)) { [void]$body.Add($l) } }
            }
            "services" {
                $out = Join-Path $work "services.txt"
                $rc = Run-Wait "cmd.exe" ('/c sc query state= all | findstr /i "1C 1cv8 Service"') $out
                [void]$body.Add("rc=" + $rc)
                if (Test-Path $out) { foreach ($l in (Get-Content $out)) { [void]$body.Add($l) } }
            }
            "copy-log" {
                $out = Join-Path $drop ("applog-" + $Name + "-" + $id + ".log")
                $src = Get-AppLogPath
                if ($src -ne "") { Copy-Item $src $out -Force; [void]$body.Add("скопирован: " + $out) }
                else { [void]$body.Add("журнал приложения не найден") }
            }
            "start-ras" {
                $ras = ""
                $roots = @("C:\Program Files\1cv8", "C:\Program Files (x86)\1cv8")
                foreach ($r in $roots) {
                    if (Test-Path $r) {
                        $dirs = Get-ChildItem $r -ErrorAction SilentlyContinue | Where-Object { $_.PSIsContainer } | Sort-Object Name -Descending
                        foreach ($dd in $dirs) {
                            $probe = Join-Path $dd.FullName "bin\ras.exe"
                            if (Test-Path $probe) { $ras = $probe; break }
                        }
                    }
                    if ($ras -ne "") { break }
                }
                if ($ras -eq "") { [void]$body.Add("ras.exe не найден") }
                else {
                    $sh = New-Object -ComObject WScript.Shell
                    [void]$sh.Run('"' + $ras + '" --port=1545', 0, $false)
                    Start-Sleep -Seconds 4
                    [void]$body.Add("запущен: " + $ras + " --port=1545")
                }
            }
            default {
                [void]$body.Add("НЕИЗВЕСТНАЯ КОМАНДА: " + $command)
                [void]$body.Add("Разрешено: noop, digest, navtest, racprobe, netstat, services, copy-log, start-ras")
            }
        }

        # Свежий журнал приложения рядом с результатом: видно, на каком шаге что пошло не так.
        try {
            $src = Get-AppLogPath
            if ($src -ne "") { Copy-Item $src (Join-Path $drop ("applog-" + $Name + ".log")) -Force }
            $crash = Join-Path $env:TEMP "1c-license-inspector-crash.log"
            if (Test-Path $crash) { Copy-Item $crash (Join-Path $drop ("crashlog-" + $Name + ".log")) -Force }
        } catch { }

        $body | Set-Content -Path $resultFile -Encoding UTF8
        Move-Item $task.FullName (Join-Path $done ($id + ".task")) -Force
        Write-Log ("готово " + $id + " → " + $resultFile)
    }
    catch {
        Write-Log ("ошибка: " + $_.Exception.Message)
        Start-Sleep -Milliseconds 800
    }
}
