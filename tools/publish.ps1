# Публикация проекта на GitHub: сборка -> скан секретов -> коммит -> пуш.
# Использование:
#   .\tools\publish.ps1 -Message "Что изменилось"
#   .\tools\publish.ps1 -Message "..." -SkipBuild
#   .\tools\publish.ps1 -Message "..." -NoPush
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [switch]$SkipBuild,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Step([string]$text) { Write-Host ('== ' + $text) -ForegroundColor Cyan }

# 1. Сборка
if (-not $SkipBuild) {
    Step 'Сборка'
    & (Join-Path $root 'build.ps1') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Сборка не удалась' }
if (Test-Path (Join-Path $root 'build\Mmc20Dump.exe')) { Copy-Item (Join-Path $root 'build\Mmc20Dump.exe') (Join-Path $root 'dist\Mmc20Dump.exe') -Force }
if (Test-Path (Join-Path $root 'build\Worker.exe')) { Copy-Item (Join-Path $root 'build\Worker.exe') (Join-Path $root 'dist\Worker.exe') -Force }
}

# 2. Скан секретов в том, что попадёт в коммит
Step 'Скан секретов'
$patterns = '192\.168\.|10\.[0-9]+\.[0-9]+\.[0-9]+|\b[0-9]{10}\b|password\s*=|passwd|token\s*=|secret\s*=|BEGIN .*PRIVATE KEY'
$allowed = '6595b64144ccf1df|bigsprut@mail\.ru|1234567890|0000000000|1111111111'
$hits = @()
Get-ChildItem $root -Recurse -File -Include *.cs,*.ps1,*.md,*.txt,*.manifest |
    Where-Object { $_.FullName -notmatch '\\dist\\|\\build\\|\\drop\\|\\queue\\|\\try\\|\\.git\\|\\tools\\publish\.ps1$' } |
    ForEach-Object {
        $file = $_
        Select-String -Path $file.FullName -Pattern $patterns -AllMatches -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch $allowed } |
            ForEach-Object { $hits += ($file.FullName.Replace($root + '\', '') + ':' + $_.LineNumber + ': ' + $_.Line.Trim()) }
    }
if ($hits.Count -gt 0) {
    $hits | ForEach-Object { Write-Host ('  ! ' + $_) -ForegroundColor Red }
    throw ('Найдено похожее на секреты: ' + $hits.Count + ' строк. Коммит остановлен.')
}
Write-Host '  чисто' -ForegroundColor Green

# 3. Коммит и пуш
Step 'Коммит'
git add -A
$staged = git status --short
if (-not $staged) {
    Write-Host '  изменений нет — коммит не нужен' -ForegroundColor Yellow
    return
}
git commit -m $Message | Out-Host

if ($NoPush) { Write-Host '  пуш пропущен (-NoPush)' -ForegroundColor Yellow; return }

Step 'Пуш'
git push | Out-Host
git log --oneline -1 | Out-Host
