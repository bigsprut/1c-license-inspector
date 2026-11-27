# Подставляет версию из тега в исходники перед сборкой в GitHub Actions.
# Пример: .\tools\ci-prepare-version.ps1 -Tag v1.2.0
param(
    [Parameter(Mandatory = $true)][string]$Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if ($Tag -notmatch '^v?(\d+)\.(\d+)\.(\d+)$') {
    throw ('Некорректный тег версии: ' + $Tag + ' (ожидается vX.Y.Z)')
}
$version = $Matches[1] + '.' + $Matches[2] + '.' + $Matches[3]

$program = Join-Path $root 'src\Program.cs'
$text = [System.IO.File]::ReadAllText($program, [System.Text.Encoding]::UTF8)
$text = [regex]::Replace($text, 'public const string Version = "[^"]*"', 'public const string Version = "' + $version + '"')
[System.IO.File]::WriteAllText($program, $text, (New-Object System.Text.UTF8Encoding($false)))

$assembly = Join-Path $root 'src\AssemblyInfo.cs'
$text = [System.IO.File]::ReadAllText($assembly, [System.Text.Encoding]::UTF8)
$text = [regex]::Replace($text, 'AssemblyVersion\("\d+\.\d+\.\d+\.\d+"\)', 'AssemblyVersion("' + $version + '.0")')
$text = [regex]::Replace($text, 'AssemblyFileVersion\("\d+\.\d+\.\d+\.\d+"\)', 'AssemblyFileVersion("' + $version + '.0")')
[System.IO.File]::WriteAllText($assembly, $text, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ('Версия из тега: ' + $version)
