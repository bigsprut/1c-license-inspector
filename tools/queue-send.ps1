# Отправка задания обработчику на сервере и ожидание результата (запускается на моей стороне).
# Задание кладётся в queue\<сервер>\<id>.task, результат читается из drop\result-<сервер>-<id>.txt.
#
# Примеры:
#   .\tools\queue-send.ps1 -Command noop -Server all
#   .\tools\queue-send.ps1 -Command digest -Server s200 -TimeoutSec 300
#   .\tools\queue-send.ps1 -Command navtest -Server all -NoWait
param(
    [Parameter(Mandatory = $true)][string]$Command,
    [ValidateSet("all", "s200")][string]$Server = "all",
    [string]$Root = "",
    [int]$TimeoutSec = 180,
    [switch]$NoWait
)

$ErrorActionPreference = "Stop"
if ($Root -eq "") { $Root = Split-Path -Parent $PSScriptRoot }
if ($Root -eq "") { $Root = (Get-Location).Path }

$queue = Join-Path $Root ("queue\" + $Server)
$drop = Join-Path $Root "drop"
foreach ($d in @($queue, $drop)) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null } }

$id = (Get-Date).ToString("yyyyMMdd-HHmmss-fff")
$taskFile = Join-Path $queue ($id + ".task")
$resultFile = Join-Path $drop ("result-" + $Server + "-" + $id + ".txt")

Set-Content -Path $taskFile -Value $Command -Encoding UTF8
Write-Host ("задание отправлено: " + $Server + " · " + $Command + " · id=" + $id)
if ($NoWait) { exit 0 }

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $resultFile) {
        Start-Sleep -Milliseconds 200
        Get-Content $resultFile -Encoding UTF8
        exit 0
    }
    Start-Sleep -Milliseconds 400
}

Write-Host ("нет результата за " + $TimeoutSec + " с.")
$hb = Join-Path $drop ("worker-" + $Server + ".txt")
if (Test-Path $hb) { Write-Host ("последний heartbeat: " + (Get-Content $hb -Encoding UTF8)) }
else { Write-Host ("обработчик на сервере " + $Server + " не запущен (нет файла " + $hb + ")") }
exit 1
