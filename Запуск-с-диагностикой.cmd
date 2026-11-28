@echo off
setlocal
title Worker start with diagnostics

rem Auto-detect queue name by host name.
set "NAME=all"
echo %COMPUTERNAME% | findstr /i "2003" >nul && set "NAME=s200"

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "WORKER=%ROOT%\tools\server-worker.ps1"
if not exist "%WORKER%" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
if not exist "%WORKER%" set "WORKER=%ROOT%\tools\server-worker.ps1"

set "DROP=%ROOT%\drop"
if not exist "%DROP%" mkdir "%DROP%"
set "DIAG=%DROP%\diag-%COMPUTERNAME%-%NAME%.txt"

echo === diagnostics %DATE% %TIME% === > "%DIAG%"
echo Host        : %COMPUTERNAME% >> "%DIAG%"
ver >> "%DIAG%"
echo Root        : %ROOT% >> "%DIAG%"
echo Worker      : %WORKER% >> "%DIAG%"
if exist "%WORKER%" (echo Worker file : found >> "%DIAG%") else (echo Worker file : MISSING >> "%DIAG%")

echo --- PowerShell version --------------- >> "%DIAG%"
powershell -NoProfile -Command "$h = Get-Host; $h.Version.ToString()" >> "%DIAG%" 2>&1
echo --- -ExecutionPolicy supported? ----- >> "%DIAG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Write-Output policy-ok" >> "%DIAG%" 2>&1
echo --- TEMP writable? ------------------ >> "%DIAG%"
powershell -NoProfile -Command "Set-Content -Path ($env:TEMP + '\probe.txt') -Value 'ok'; if (Test-Path ($env:TEMP + '\probe.txt')) { 'temp-ok' } else { 'temp-FAIL' }" >> "%DIAG%" 2>&1

rem PowerShell 1.0 ?? ????? -ExecutionPolicy: ????????? ????? ?????? ?????????????.
set "PSFLAGS=-NoProfile -ExecutionPolicy Bypass"
powershell -NoProfile -ExecutionPolicy Bypass -Command "exit 0" >nul 2>&1
if errorlevel 1 set "PSFLAGS=-NoProfile"
echo --- PS flags: %PSFLAGS% --------------- >> "%DIAG%"

echo --- starting worker ------------------ >> "%DIAG%"
echo.
echo Diagnostics are being written to:
echo   %DIAG%
echo.
echo Starting worker (queue: %NAME%) ...
echo.

powershell %PSFLAGS% -File "%WORKER%" -Name %NAME% -Root "%ROOT%" >> "%DIAG%" 2>&1
set "RC=%ERRORLEVEL%"
echo === worker exited, code=%RC% === >> "%DIAG%"
echo.
echo Worker exited with code %RC%. Full report:
echo   %DIAG%
echo.
type "%DIAG%"
pause