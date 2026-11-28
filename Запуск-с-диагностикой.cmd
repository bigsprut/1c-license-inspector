@echo off
setlocal
title License inspector worker + diagnostics

set "NAME=all"
echo %COMPUTERNAME% | findstr /i "2003" >nul && set "NAME=s200"
if not "%~1"=="" set "NAME=%~1"

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
if not exist "%ROOT%\build\Worker.exe" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
set "WORKER=%ROOT%\build\Worker.exe"
set "DROP=%ROOT%\drop"
if not exist "%DROP%" mkdir "%DROP%"
set "DIAG=%DROP%\diag-%COMPUTERNAME%-%NAME%.txt"

echo === diagnostics %DATE% %TIME% === > "%DIAG%"
echo Host        : %COMPUTERNAME% >> "%DIAG%"
ver >> "%DIAG%"
echo Root        : %ROOT% >> "%DIAG%"
echo Worker      : %WORKER% >> "%DIAG%"
if exist "%WORKER%" (echo Worker exe  : found >> "%DIAG%") else (echo Worker exe  : MISSING >> "%DIAG%")
if exist "%ROOT%\build\1C-License-Inspector.exe" (echo App exe     : found >> "%DIAG%") else (echo App exe     : MISSING >> "%DIAG%")

echo --- .NET Framework ------------------ >> "%DIAG%"
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Version >> "%DIAG%" 2>&1
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client" /v Version >> "%DIAG%" 2>&1

echo --- PowerShell (informational) ------ >> "%DIAG%"
powershell -NoProfile -Command "$h = Get-Host; $h.Version.ToString()" >> "%DIAG%" 2>&1

echo --- starting worker ------------------ >> "%DIAG%"
"%WORKER%" %NAME% "%ROOT%" >> "%DIAG%" 2>&1
set "RC=%ERRORLEVEL%"
echo === worker exited, code=%RC% === >> "%DIAG%"
type "%DIAG%"
pause